using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using OElite.Abstractions;
using OElite.Utils;

namespace OElite.Providers
{
    /// <summary>
    /// AWS S3 implementation of ICacheProvider
    /// Uses S3 as a cache layer, useful for CDN scenarios
    /// </summary>
    public class S3CacheProvider : ICacheProvider
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly RestConfig _config;

        public S3CacheProvider(string connectionString, RestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            try
            {
                // Parse connection string to extract S3 configuration
                var s3Config = ParseConnectionString(connectionString);
                
                // Create S3 client configuration
                var clientConfig = new AmazonS3Config
                {
                    ServiceURL = s3Config.ServiceUrl,
                    ForcePathStyle = s3Config.ForcePathStyle,
                    UseHttp = s3Config.UseHttp
                };
                
                // Set region if provided and no custom service URL
                if (string.IsNullOrEmpty(s3Config.ServiceUrl) && s3Config.Region != null)
                {
                    clientConfig.RegionEndpoint = s3Config.Region;
                }
                
                _s3Client = new AmazonS3Client(s3Config.AccessKeyId, s3Config.SecretAccessKey, clientConfig);
                _bucketName = s3Config.BucketName ?? "restme-cache";
                
                // Ensure bucket exists
                EnsureBucketExistsAsync().Wait();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize S3 cache provider: {ex.Message}", ex);
            }
        }

        private S3Config ParseConnectionString(string connectionString)
        {
            var config = new S3Config();
            var parts = connectionString.Split(';');
            
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;
                
                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();
                
                switch (key)
                {
                    case "accesskeyid":
                        config.AccessKeyId = value;
                        break;
                    case "secretaccesskey":
                        config.SecretAccessKey = value;
                        break;
                    case "region":
                        config.Region = Amazon.RegionEndpoint.GetBySystemName(value);
                        break;
                    case "bucketname":
                        config.BucketName = value;
                        break;
                    case "serviceurl":
                    case "endpoint":
                        config.ServiceUrl = value;
                        break;
                    case "forcepathstyle":
                        config.ForcePathStyle = bool.Parse(value);
                        break;
                    case "usehttp":
                        config.UseHttp = bool.Parse(value);
                        break;
                }
            }
            
            if (string.IsNullOrEmpty(config.AccessKeyId) || string.IsNullOrEmpty(config.SecretAccessKey))
                throw new ArgumentException("AccessKeyId and SecretAccessKey are required in connection string");
            
            // Set defaults for S3-compatible providers
            if (config.Region == null && string.IsNullOrEmpty(config.ServiceUrl))
                config.Region = Amazon.RegionEndpoint.USEast1;
                
            return config;
        }

        private async Task EnsureBucketExistsAsync()
        {
            try
            {
                var request = new GetBucketLocationRequest
                {
                    BucketName = _bucketName
                };
                await _s3Client.GetBucketLocationAsync(request);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Bucket doesn't exist, create it
                var createRequest = new PutBucketRequest
                {
                    BucketName = _bucketName,
                    BucketRegion = S3Region.USEast1,
                    CannedACL = S3CannedACL.Private
                };
                await _s3Client.PutBucketAsync(createRequest);
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                using var response = await _s3Client.GetObjectAsync(request);
                using var reader = new StreamReader(response.ResponseStream);
                var json = await reader.ReadToEndAsync();
                
                return json.JsonDeserialize<T>();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // Key doesn't exist
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (value == null)
                return false;

            try
            {
                var json = value.JsonSerialize();
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    ContentBody = json,
                    ContentType = "application/json",
                    CannedACL = S3CannedACL.Private
                };

                // Set cache control headers for CDN scenarios
                request.Headers.CacheControl = "public, max-age=3600"; // Default 1 hour
                
                if (expiry.HasValue)
                {
                    var maxAge = (int)expiry.Value.TotalSeconds;
                    request.Headers.CacheControl = $"public, max-age={maxAge}";
                    
                    // Set metadata for expiry tracking
                    var expiryTime = DateTime.UtcNow.Add(expiry.Value);
                    request.Metadata.Add("expiry", expiryTime.ToString("O"));
                }

                await _s3Client.PutObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                await _s3Client.DeleteObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to check if cached item exists with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                // First, get the object to copy its content
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                using var getResponse = await _s3Client.GetObjectAsync(getRequest);
                using var reader = new StreamReader(getResponse.ResponseStream);
                var content = await reader.ReadToEndAsync();

                // Copy the object with new metadata
                var copyRequest = new CopyObjectRequest
                {
                    SourceBucket = _bucketName,
                    SourceKey = key,
                    DestinationBucket = _bucketName,
                    DestinationKey = key,
                    MetadataDirective = S3MetadataDirective.REPLACE
                };

                // Copy existing metadata
                if (getResponse.Metadata != null)
                {
                    foreach (var metadataKey in getResponse.Metadata.Keys)
                    {
                        copyRequest.Metadata.Add(metadataKey, getResponse.Metadata[metadataKey]);
                    }
                }

                // Update cache control and expiry
                var maxAge = (int)expiry.TotalSeconds;
                copyRequest.Headers.CacheControl = $"public, max-age={maxAge}";
                
                var expiryTime = DateTime.UtcNow.Add(expiry);
                copyRequest.Metadata["expiry"] = expiryTime.ToString("O");

                await _s3Client.CopyObjectAsync(copyRequest);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false; // Key doesn't exist
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set expiry for cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
        {
            if (responseMessage?.Data == null)
                return null;

            try
            {
                if (responseMessage.Data is T directData)
                    return directData;

                if (responseMessage.Data is string jsonString)
                    return jsonString.JsonDeserialize<T>();

                return responseMessage.Data.JsonSerialize().JsonDeserialize<T>();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }

        private class S3Config
        {
            public string AccessKeyId { get; set; } = string.Empty;
            public string SecretAccessKey { get; set; } = string.Empty;
            public Amazon.RegionEndpoint? Region { get; set; }
            public string? BucketName { get; set; }
            public string? ServiceUrl { get; set; }
            public bool ForcePathStyle { get; set; } = false;
            public bool UseHttp { get; set; } = false;
        }
    }
}
