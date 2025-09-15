using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OElite.Abstractions;
using OElite.Utils;
using Amazon.S3;
using Amazon.S3.Model;

namespace OElite.Providers
{
    /// <summary>
    /// S3 implementation of IStorageProvider
    /// </summary>
    public class S3StorageProvider : BaseStorageProvider
    {
        private readonly AmazonS3Client _s3Client;
        private readonly S3Configuration _s3Config;

        public S3StorageProvider(string connectionString, RestConfig config) : base(config)
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
            
            // Use credentials from config or parsed connection string
            var accessKey = !string.IsNullOrEmpty(_s3Config.AccessKeyId) ? _s3Config.AccessKeyId : config.RestKey;
            var secretKey = !string.IsNullOrEmpty(_s3Config.SecretAccessKey) ? _s3Config.SecretAccessKey : config.RestSecret;
            
            _s3Client = new AmazonS3Client(accessKey, secretKey, clientConfig);
        }

        public override async Task<T> GetAsync<T>(string key) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
                };

                using var response = await _s3Client.GetObjectAsync(request);
                
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return HandleStreamType<T>(response.ResponseStream);
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

        public override async Task<T> PutAsync<T>(string key, T value) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutAsync");
            ValidateValue(value, "PutAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
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
                    var jsonValue = value.JsonSerialize(Config.UseRestConvertForCollectionSerialization, Config.SerializerSettings);
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

        public override async Task<bool> DeleteAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "DeleteAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
                };

                await _s3Client.DeleteObjectAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to delete S3 object '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "ExistsAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    return false;

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
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

        public override async Task<string?> GetStringAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStringAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
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

        public override async Task<string?> PutStringAsync(string key, string value)
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutStringAsync");
            ValidateValue(value, "PutStringAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey,
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

        public override async Task<Stream?> GetStreamAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStreamAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey
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

        public override async Task<bool> PutStreamAsync(string key, Stream stream)
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutStreamAsync");
            ValidateValue(stream, "PutStreamAsync");

            try
            {
                var bucketName = S3ConnectionStringParser.GetBucketName(key);
                var objectKey = S3ConnectionStringParser.GetObjectKey(key);
                
                if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = finalObjectKey,
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

        public override async Task<T> GetStreamAsync<T>(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStreamAsync");

            try
            {
                var stream = await GetStreamAsync(key);
                return HandleStreamTypeForStream<T>(stream);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get S3 object stream as type '{typeof(T).Name}' for '{key}': {ex.Message}", ex);
            }
        }

        public override void Dispose()
        {
            if (!Disposed)
            {
                _s3Client?.Dispose();
                Disposed = true;
            }
        }
    }
}
