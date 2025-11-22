using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using OElite;
using Amazon.Runtime;
using OElite.Restme;
using OElite.Restme.Abstractions;

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

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public override string ProviderName => "S3Cache";

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Cache;

        public S3CacheProvider(RestConfig config) : base(config)
        {
            // Use pre-parsed config values directly
            var accessKey = config.AuthKey;
            var secretKey = config.AuthSecret;

            // Always parse connection string if provided to get endpoint and configuration settings
            S3Configuration? parsedConfig = null;
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                parsedConfig = S3ConnectionStringParser.ParseConnectionString(config);

                // Use connection string credentials as fallback if not provided in config
                accessKey = accessKey ?? parsedConfig.AccessKeyId;
                secretKey = secretKey ?? parsedConfig.SecretAccessKey;
            }

            // Validate that we have credentials from either config or connection string
            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException(
                    "S3 credentials not provided. Set AuthKey and AuthSecret in RestConfig, or provide a connection string.");
            }


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
            _bucketName = config.InstanceName ?? parsedConfig?.BucketName ?? "restme-cache";
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


        public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
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

                // Check for expiry in object metadata
                if (response.Metadata.Count > 0)
                {
                    // S3 metadata keys are case-insensitive and may have the x-amz-meta- prefix stripped
                    var expiryKey = response.Metadata.Keys.FirstOrDefault(k =>
                        k.Equals("expiry-utc", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("x-amz-meta-expiry-utc", StringComparison.OrdinalIgnoreCase));

                    if (expiryKey != null)
                    {
                        var expiryString = response.Metadata[expiryKey];
                        if (TryParseExpiryTime(expiryString, out var expiryTime) && DateTime.UtcNow > expiryTime)
                        {
                            // Object has expired, remove it and return null
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await RemoveAsync(key, cancellationToken);
                                }
                                catch
                                {
                                    // Ignore cleanup failures
                                }
                            }, cancellationToken);
                            return null;
                        }
                    }
                }

                // Read content from S3
                using var reader = new StreamReader(response.ResponseStream);
                var content = await reader.ReadToEndAsync();

                // Handle string type directly, otherwise deserialize from JSON
                if (typeof(T) == typeof(string))
                {
                    return content as T;
                }

                return StringUtils.JsonDeserialize<T>(content);
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


        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "SetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = S3ConnectionStringParser.CombinePath(_s3Config.RootPath, key);

                // Store user data directly without tampering
                string content;
                if (value is string stringValue)
                {
                    content = stringValue;
                }
                else
                {
                    content = StringUtils.JsonSerialize(value);
                }

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = finalKey,
                    ContentBody = content,
                    ContentType = value is string ? "text/plain" : "application/json"
                };

                // Set cache control headers for expiry (informational)
                if (expiry.HasValue)
                {
                    var expiryTime = DateTime.UtcNow.Add(expiry.Value);
                    request.Headers["Cache-Control"] = $"max-age={expiry.Value.TotalSeconds:F0}";
                    request.Headers["Expires"] = expiryTime.ToString("R");

                    // Store expiry in object metadata for application-level validation
                    request.Metadata["x-amz-meta-expiry-utc"] = expiryTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
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
                var response = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

                // Check for expiry in object metadata
                if (response.Metadata.Count > 0)
                {
                    // S3 metadata keys are case-insensitive and may have the x-amz-meta- prefix stripped
                    var expiryKey = response.Metadata.Keys.FirstOrDefault(k =>
                        k.Equals("expiry-utc", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("x-amz-meta-expiry-utc", StringComparison.OrdinalIgnoreCase));

                    if (expiryKey != null)
                    {
                        var expiryString = response.Metadata[expiryKey];
                        if (TryParseExpiryTime(expiryString, out var expiryTime) && DateTime.UtcNow > expiryTime)
                        {
                            // Object has expired, remove it and return false
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await RemoveAsync(key, cancellationToken);
                                }
                                catch
                                {
                                    // Ignore cleanup failures
                                }
                            }, cancellationToken);
                            return false;
                        }
                    }
                }

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


        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry,
            CancellationToken cancellationToken = default)
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

                var expiryTime = DateTime.UtcNow.Add(expiry);
                copyRequest.Metadata.Add("Cache-Control", $"max-age={expiry.TotalSeconds:F0}");
                copyRequest.Metadata.Add("Expires", expiryTime.ToString("R"));
                copyRequest.Metadata.Add("x-amz-meta-expiry-utc", expiryTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

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
        /// Parses the expiry time string stored in S3 metadata with proper UTC handling
        /// </summary>
        private static bool TryParseExpiryTime(string expiryString, out DateTime expiryTime)
        {
            // Try parsing as exact UTC format first (the format we store)
            if (DateTime.TryParseExact(expiryString, "yyyy-MM-ddTHH:mm:ss.fffZ",
                null, System.Globalization.DateTimeStyles.RoundtripKind, out expiryTime))
            {
                return true;
            }

            // Fallback to general parsing with UTC assumption
            if (DateTime.TryParse(expiryString, null, System.Globalization.DateTimeStyles.AssumeUniversal, out expiryTime))
            {
                expiryTime = expiryTime.ToUniversalTime();
                return true;
            }

            expiryTime = default;
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