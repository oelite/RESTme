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
            
            var s3Config = new AmazonS3Config
            {
                ServiceURL = connectionString,
                ForcePathStyle = true, // Required for S3-compatible services
                UseHttp = !config.RestSsl
            };

            _s3Client = new AmazonS3Client(config.RestKey, config.RestSecret, s3Config);
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _s3Client?.Dispose();
                _disposed = true;
            }
        }
    }
}
