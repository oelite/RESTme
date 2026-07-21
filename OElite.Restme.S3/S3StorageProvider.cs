using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using OElite.Restme;
using OElite.Restme.Abstractions;

namespace OElite.Providers
{
    /// <summary>
    /// S3 implementation of IStorageProvider
    /// </summary>
    public class S3StorageProvider : BaseStorageProvider
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly S3Configuration _s3Config;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public override string ProviderName => "S3Storage";

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Storage;

        public S3StorageProvider(RestConfig config) : base(config)
        {
            // Always parse connection string for endpoint and configuration
            S3Configuration? parsedConfig = null;
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                try
                {
                    parsedConfig = S3ConnectionStringParser.ParseConnectionString(config);
                }
                catch (ArgumentException)
                {
                    // If connection string parsing fails, continue with RestConfig values only
                    parsedConfig = null;
                }
            }

            // Prioritize RestConfig authentication fields, fallback to parsed credentials
            var accessKey = config.AuthKey ?? parsedConfig?.AccessKeyId;
            var secretKey = config.AuthSecret ?? parsedConfig?.SecretAccessKey;


            // Create AWS S3 client configuration
            var s3Config = new AmazonS3Config();

            // Set region FIRST - must be done before setting ServiceURL to prevent AWS SDK from overriding
            if (!string.IsNullOrEmpty(config.Region))
            {
                s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
            }
            else if (parsedConfig?.Region != null)
            {
                s3Config.RegionEndpoint = parsedConfig.Region;
            }
            else
            {
                s3Config.RegionEndpoint = RegionEndpoint.USEast1; // Default
            }

            // Set additional S3 config from parsed connection string if available (before ServiceURL)
            if (parsedConfig != null)
            {
                s3Config.ForcePathStyle = parsedConfig.ForcePathStyle;
                s3Config.UseHttp = parsedConfig.UseHttp;
            }

            // Set ServiceURL LAST - after region and other settings to prevent overriding
            if (!string.IsNullOrEmpty(config.Endpoint))
            {
                s3Config.ServiceURL = config.Endpoint;
            }
            else if (parsedConfig?.ServiceUrl != null)
            {
                s3Config.ServiceURL = parsedConfig.ServiceUrl;
            }

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
            _bucketName = config.InstanceName ?? parsedConfig?.BucketName ?? "restme-storage";
            _s3Config = parsedConfig ?? new S3Configuration { RootPath = config.RootPath };

            // Ensure bucket exists synchronously for reliable initialization
            try
            {
                EnsureBucketExistsAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Warning: Could not ensure bucket exists: {ex.Message}");
                // Continue anyway - bucket might be created later or already exist
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
                    BucketName = _bucketName
                };
                await _s3Client.PutBucketAsync(createRequest);
            }
        }

        public override async Task<T?> GetAsync<T>(string objectKey, CancellationToken cancellationToken = default)
            where T : class
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                using var response = await _s3Client.GetObjectAsync(request, cancellationToken);

                // Copy the response stream to a MemoryStream since AWS S3 ResponseStream doesn't support seeking
                using var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                return HandleStreamType<T>(memoryStream);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new OEliteException($"S3 object '{objectKey}' does not exist or is not accessible: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<T?> PutAsync<T>(string objectKey, T value,
            CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                await HandleStreamPutAsync(value, async stream => { request.InputStream = stream; });

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.PutObjectAsync(request, cancellationToken);
                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to put S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "DeleteAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.DeleteObjectAsync(request, cancellationToken);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to delete S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "ExistsAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    return false;

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
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
                throw new OEliteException($"Failed to check if S3 object '{objectKey}' exists: {ex.Message}", ex);
            }
        }

        public override async Task<string?> GetStringAsync(string objectKey,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStringAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
                using var reader = new StreamReader(response.ResponseStream);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new OEliteException($"S3 object '{objectKey}' does not exist or is not accessible: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> PutStringAsync(string objectKey, string value,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutStringAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey,
                    ContentBody = value,
                    ContentType = "text/plain"
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.PutObjectAsync(request, cancellationToken);
                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to put S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<Stream?> GetStreamAsync(string objectKey,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStreamAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                var response = await _s3Client.GetObjectAsync(request, cancellationToken);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new OEliteException($"S3 object '{objectKey}' does not exist or is not accessible: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> PutStreamAsync(string objectKey, Stream stream,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutStreamAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey,
                    InputStream = stream
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _s3Client.PutObjectAsync(request, cancellationToken);
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to put S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<T> GetStreamAsync<T>(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStreamAsync");

            try
            {
                if (objectKey.IsNullOrEmpty())
                    throw new OEliteException("Invalid S3 path. Expected format: bucket-name/object-key");

                // Apply root path if specified
                var finalObjectKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, objectKey);

                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalObjectKey
                };

                cancellationToken.ThrowIfCancellationRequested();
                using var response = await _s3Client.GetObjectAsync(request, cancellationToken);

                // Copy the response stream to a MemoryStream since AWS S3 ResponseStream doesn't support seeking
                using var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                return HandleStreamTypeForStream<T>(memoryStream);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new OEliteException($"S3 object '{objectKey}' does not exist or is not accessible: {ex.Message}",
                    ex);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get S3 object '{objectKey}': {ex.Message}", ex);
            }
        }

        public override void Dispose()
        {
            _s3Client?.Dispose();
        }
    }
}