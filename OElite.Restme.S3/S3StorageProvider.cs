using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OElite.Abstractions;
using Amazon.S3;
using Amazon.S3.Model;

namespace OElite.Providers
{
    /// <summary>
    /// S3 implementation of IStorageProvider
    /// </summary>
    public class S3StorageProvider : IStorageProvider
    {
        private readonly AmazonS3Client _s3Client;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public S3StorageProvider(string connectionString, RestConfig config)
        {
            _config = config;
            
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
            
            // Use credentials from config or parsed connection string
            var accessKey = !string.IsNullOrEmpty(s3Config.AccessKeyId) ? s3Config.AccessKeyId : config.RestKey;
            var secretKey = !string.IsNullOrEmpty(s3Config.SecretAccessKey) ? s3Config.SecretAccessKey : config.RestSecret;
            
            _s3Client = new AmazonS3Client(accessKey, secretKey, clientConfig);
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                using var response = await _s3Client.GetObjectAsync(request);
                
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    var bytes = FileUtils.ReadStreamToEnd(response.ResponseStream);
                    
                    T? result;
                    if (typeof(T).GetTypeInfo().IsAbstract)
                    {
                        result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
                    }
                    else
                        result = (T)Activator.CreateInstance(typeof(T), bytes)!;

                    return result;
                }

                using var reader = new StreamReader(response.ResponseStream);
                var content = await reader.ReadToEndAsync();
                
                if (!content.IsNotNullOrEmpty())
                    return null;

                if (typeof(T) == typeof(string))
                    return (T)Convert.ChangeType(content, typeof(T));

                return content.JsonDeserialize<T>();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get S3 object '{key}': {ex.Message}", ex);
            }
        }

        public async Task<T?> PutAsync<T>(string key, T value) where T : class
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                // Set content type based on file extension
                var extension = FileUtils.GetFileExtensionName(key);
                if (extension.IsNotNullOrEmpty())
                    request.ContentType = FileUtils.GetMimeType(extension);

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    if (value is not Stream stream)
                        return value;
                    
                    stream.Position = 0;
                    request.InputStream = stream;
                }
                else
                {
                    var jsonValue = value.JsonSerialize(_config.UseRestConvertForCollectionSerialization, _config.SerializerSettings);
                    request.ContentBody = jsonValue;
                    request.ContentType = "application/json";
                }

                await _s3Client.PutObjectAsync(request);
                return value;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload S3 object '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                await _s3Client.DeleteObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to delete S3 object '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    return false;

                var request = new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
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
                throw new OEliteWebException($"Failed to check S3 object existence '{key}': {ex.Message}", ex);
            }
        }

        public async Task<string?> GetStringAsync(string key)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                using var response = await _s3Client.GetObjectAsync(request);
                using var reader = new StreamReader(response.ResponseStream);
                
                return await reader.ReadToEndAsync();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get S3 object string '{key}': {ex.Message}", ex);
            }
        }

        public async Task<string?> PutStringAsync(string key, string value)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    ContentBody = value,
                    ContentType = "text/plain"
                };

                await _s3Client.PutObjectAsync(request);
                return value;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload S3 object string '{key}': {ex.Message}", ex);
            }
        }

        public async Task<Stream?> GetStreamAsync(string key)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                var response = await _s3Client.GetObjectAsync(request);
                var stream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(stream);
                stream.Position = 0;
                
                return stream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get S3 object stream '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> PutStreamAsync(string key, Stream stream)
        {
            try
            {
                var bucketName = GetBucketName(key);
                var objectKey = GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    InputStream = stream
                };

                // Set content type based on file extension
                var extension = FileUtils.GetFileExtensionName(key);
                if (extension.IsNotNullOrEmpty())
                    request.ContentType = FileUtils.GetMimeType(extension);

                await _s3Client.PutObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload S3 object stream '{key}': {ex.Message}", ex);
            }
        }

        public async Task<T?> GetStreamAsync<T>(string key) where T : Stream
        {
            try
            {
                var stream = await GetStreamAsync(key);
                if (stream == null)
                    return null;

                var bytes = FileUtils.ReadStreamToEnd(stream);
                
                T? result;
                if (typeof(T).GetTypeInfo().IsAbstract)
                {
                    result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
                }
                else
                    result = (T)Activator.CreateInstance(typeof(T), bytes)!;

                return result;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get S3 object stream as type '{typeof(T).Name}' for '{key}': {ex.Message}", ex);
            }
        }

        private string? GetBucketName(string storageRelativePath)
        {
            if (storageRelativePath.IsNullOrEmpty())
                return null;
            
            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : null;
        }

        private string? GetObjectKey(string storageRelativePath)
        {
            if (storageRelativePath.IsNullOrEmpty())
                return null;
            
            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
                return null;
            
            // Join all segments except the first one (bucket name) to form the object key
            return string.Join("/", segments, 1, segments.Length - 1);
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
            
            // Set defaults for S3-compatible providers
            if (config.Region == null && string.IsNullOrEmpty(config.ServiceUrl))
                config.Region = Amazon.RegionEndpoint.USEast1;
                
            return config;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _s3Client?.Dispose();
                _disposed = true;
            }
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
