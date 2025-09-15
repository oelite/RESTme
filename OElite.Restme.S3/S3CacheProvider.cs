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
    public class S3CacheProvider : BaseCacheProvider
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly S3Configuration _s3Config;

        public S3CacheProvider(string connectionString, RestConfig config) : base(config)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            try
            {
                // Parse connection string to extract S3 configuration
                _s3Config = S3ConnectionStringParser.ParseConnectionString(connectionString);
                
                // Create S3 client configuration
                var clientConfig = new AmazonS3Config
                {
                    ServiceURL = _s3Config.ServiceUrl,
                    ForcePathStyle = _s3Config.ForcePathStyle,
                    UseHttp = _s3Config.UseHttp
                };
                
                // Set region if provided and no custom service URL
                if (string.IsNullOrEmpty(_s3Config.ServiceUrl) && _s3Config.Region != null)
                {
                    clientConfig.RegionEndpoint = _s3Config.Region;
                }
                
                _s3Client = new AmazonS3Client(_s3Config.AccessKeyId, _s3Config.SecretAccessKey, clientConfig);
                _bucketName = _s3Config.BucketName ?? "restme-cache";
                
                // Ensure bucket exists
                EnsureBucketExistsAsync().Wait();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize S3 cache provider: {ex.Message}", ex);
            }
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

        public override async Task<T?> GetAsync<T>(string key) where T : class
        {
            ValidateKey(key, "GetAsync");

            try
            {
                // Apply root path if specified
                var objectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);
                
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
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

        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            ValidateKey(key, "SetAsync");
            ValidateValue(value, "SetAsync");

            try
            {
                // Apply root path if specified
                var objectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);
                
                var json = value.JsonSerialize();
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
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

        public override async Task<bool> RemoveAsync(string key)
        {
            ValidateKey(key, "RemoveAsync");

            try
            {
                // Apply root path if specified
                var objectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);
                
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                await _s3Client.DeleteObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key)
        {
            ValidateKey(key, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var objectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);
                
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
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

        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                // Apply root path if specified
                var objectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);
                
                // First, get the object to copy its content
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                using var getResponse = await _s3Client.GetObjectAsync(getRequest);
                using var reader = new StreamReader(getResponse.ResponseStream);
                var content = await reader.ReadToEndAsync();

                // Copy the object with new metadata
                var copyRequest = new CopyObjectRequest
                {
                    SourceBucket = _bucketName,
                    SourceKey = objectKey,
                    DestinationBucket = _bucketName,
                    DestinationKey = objectKey,
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

        public override void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}
