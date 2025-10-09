using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;

namespace OElite.Providers
{
    /// <summary>
    /// S3 implementation of ICacheProvider
    /// </summary>
    public class S3CacheProvider : BaseCacheProvider
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly S3Configuration _s3Config;
        protected bool Disposed = false;

        public S3CacheProvider(string connectionString, RestConfig config) : base(config)
        {
            // Parse connection string to extract S3 configuration
            _s3Config = S3ConnectionStringParser.ParseConnectionString(connectionString);

            // Use credentials from config or parsed connection string
            var accessKey = !string.IsNullOrEmpty(_s3Config.AccessKeyId) ? _s3Config.AccessKeyId : config.RestKey;
            var secretKey = !string.IsNullOrEmpty(_s3Config.SecretAccessKey)
                ? _s3Config.SecretAccessKey
                : config.RestSecret;

            // Create AWS S3 client configuration
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _s3Config.ServiceUrl,
                ForcePathStyle = _s3Config.ForcePathStyle,
                UseHttp = _s3Config.UseHttp
            };

            if (_s3Config.Region != null)
            {
                s3Config.RegionEndpoint = _s3Config.Region;
            }

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
            _bucketName = _s3Config.BucketName ?? "restme-cache";

            // Ensure bucket exists
            _ = Task.Run(async () => await EnsureBucketExistsAsync());
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
                    BucketName = _bucketName
                };
                await _s3Client.PutBucketAsync(createRequest);
            }
        }


        public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
                return HandleStreamType<T>(response.ResponseStream);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // Cache miss
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get cached object '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "SetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalKey
                };

                await HandleStreamPutAsync(value, async stream =>
                {
                    request.InputStream = stream;
                });

                // Set cache control headers for expiry
                if (expiry.HasValue)
                {
                    request.Headers["Cache-Control"] = $"max-age={expiry.Value.TotalSeconds:F0}";
                    request.Headers["Expires"] = DateTime.UtcNow.Add(expiry.Value).ToString("R");
                }

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.PutObjectAsync(request, cancellationToken);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to cache object '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(key, "RemoveAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.DeleteObjectAsync(request, cancellationToken);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to remove cached object '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(key, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = finalKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to check if cached object '{key}' exists: {ex.Message}", ex);
            }
        }


        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                // For S3, we need to copy the object with new metadata
                var copyRequest = new CopyObjectRequest
                {
                    SourceBucket = _bucketName,
                    SourceKey = finalKey,
                    DestinationBucket = _bucketName,
                    DestinationKey = finalKey,
                    MetadataDirective = S3MetadataDirective.REPLACE
                };

                copyRequest.Metadata.Add("Cache-Control", $"max-age={expiry.TotalSeconds:F0}");
                copyRequest.Metadata.Add("Expires", DateTime.UtcNow.Add(expiry).ToString("R"));

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.CopyObjectAsync(copyRequest, cancellationToken);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to set expiry for cached object '{key}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Common implementation for handling Stream types in GetAsync
        /// </summary>
        protected T? HandleStreamType<T>(Stream responseStream) where T : class
        {
            if (!typeof(Stream).IsAssignableFrom(typeof(T)))
                return null;

            var bytes = FileUtils.ReadStreamToEnd(responseStream);

            T? result;
            if (typeof(T).GetTypeInfo().IsAbstract)
            {
                result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
            }
            else
                result = (T)Activator.CreateInstance(typeof(T), bytes)!;

            return result;
        }

        /// <summary>
        /// Common implementation for handling Stream types in PutAsync
        /// </summary>
        protected async Task<bool> HandleStreamPutAsync<T>(T value, Func<Stream, Task> uploadAction) where T : class
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                if (value is not Stream stream)
                    return false;

                stream.Position = 0;
                await uploadAction(stream);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates that the provider is not disposed
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public override void Dispose()
        {
            _s3Client?.Dispose();
            Disposed = true;
        }
    }
}