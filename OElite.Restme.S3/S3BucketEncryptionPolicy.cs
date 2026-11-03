using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using OElite.Restme;
using OElite.Restme.Abstractions;

using ObjectLockMode = OElite.Restme.Abstractions.ObjectLockMode;

namespace OElite.Providers
{
    /// <summary>
    /// S3-compatible implementation of <see cref="IBucketEncryptionPolicy"/>.
    /// Exposes server-side-encryption (SSE) and Object Lock capability surface
    /// over an <see cref="IAmazonS3"/> client, mirroring the client construction
    /// of <see cref="S3StorageProvider"/> (region first, ForcePathStyle/UseHttp from
    /// the connection string, ServiceURL last) so both surfaces talk to the exact
    /// same endpoint.
    /// </summary>
    public class S3BucketEncryptionPolicy : IBucketEncryptionPolicy
    {
        private readonly IAmazonS3 _s3Client;

        /// <summary>
        /// Builds an Amazon S3 client from an OElite <see cref="RestConfig"/>, mirroring
        /// <see cref="S3StorageProvider"/>'s construction order (region first, ForcePathStyle /
        /// UseHttp from the connection string, ServiceURL last) so the policy talks to the
        /// exact endpoint the storage provider uses.
        /// </summary>
        public static AmazonS3Client CreateAmazonClient(RestConfig config)
        {
            S3Configuration? parsedConfig = null;
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                try
                {
                    parsedConfig = S3ConnectionStringParser.ParseConnectionString(config);
                }
                catch (ArgumentException)
                {
                    parsedConfig = null;
                }
            }

            var accessKey = config.AuthKey ?? parsedConfig?.AccessKeyId;
            var secretKey = config.AuthSecret ?? parsedConfig?.SecretAccessKey;

            var s3Config = new AmazonS3Config();
            if (!string.IsNullOrEmpty(config.Region))
                s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.Region);
            else if (parsedConfig?.Region != null)
                s3Config.RegionEndpoint = parsedConfig.Region;
            else
                s3Config.RegionEndpoint = Amazon.RegionEndpoint.USEast1;

            if (parsedConfig != null)
            {
                s3Config.ForcePathStyle = parsedConfig.ForcePathStyle;
                s3Config.UseHttp = parsedConfig.UseHttp;
            }

            if (!string.IsNullOrEmpty(config.Endpoint))
                s3Config.ServiceURL = config.Endpoint;
            else if (parsedConfig?.ServiceUrl != null)
                s3Config.ServiceURL = parsedConfig.ServiceUrl;

            return new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        public S3BucketEncryptionPolicy(RestConfig config) : this(CreateAmazonClient(config))
        {
        }

        /// <summary>
        /// Seam constructor for testing and DI registration.
        /// </summary>
        public S3BucketEncryptionPolicy(IAmazonS3 s3Client)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        }

        /// <summary>
        /// Query the storage backend for the bucket's default encryption status.
        /// </summary>
        public async Task<BucketEncryptionStatusResult> GetBucketEncryptionStatusAsync(
            string bucketName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));

            try
            {
                var response = await _s3Client.GetBucketEncryptionAsync(
                    new GetBucketEncryptionRequest { BucketName = bucketName },
                    cancellationToken);

                var rules = response.ServerSideEncryptionConfiguration?.ServerSideEncryptionRules;
                if (rules is { Count: > 0 } &&
                    rules[0].ServerSideEncryptionByDefault?.ServerSideEncryptionAlgorithm != null)
                {
                    var byDefault = rules[0].ServerSideEncryptionByDefault;
                    var algorithm = byDefault.ServerSideEncryptionAlgorithm.Value;
                    var kmsKeyId = string.IsNullOrWhiteSpace(byDefault.ServerSideEncryptionKeyManagementServiceKeyId)
                        ? null
                        : byDefault.ServerSideEncryptionKeyManagementServiceKeyId;

                    return new BucketEncryptionStatusResult(
                        BucketEncryptionStatus.Enabled,
                        algorithm,
                        kmsKeyId,
                        $"GetBucketEncryption returned {rules.Count} rule(s); algorithm={algorithm ?? "unspecified"}",
                        DateTimeOffset.UtcNow);
                }

                return new BucketEncryptionStatusResult(
                    BucketEncryptionStatus.Disabled,
                    null,
                    null,
                    "GetBucketEncryption succeeded but returned no default-encryption rules",
                    DateTimeOffset.UtcNow);
            }
            catch (AmazonS3Exception ex)
            {
                return new BucketEncryptionStatusResult(
                    ClassifyS3Error(ex),
                    null,
                    null,
                    $"S3 API error: HTTP {(int)ex.StatusCode} {ex.StatusCode}, error code {ex.ErrorCode ?? "none"}",
                    DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                return new BucketEncryptionStatusResult(
                    BucketEncryptionStatus.Unavailable,
                    null,
                    null,
                    $"Probe failed before an S3 response was received: {ex.GetType().Name}",
                    DateTimeOffset.UtcNow);
            }
        }

        /// <summary>
        /// Apply default bucket encryption (SSE-S3 or SSE-KMS) and re-verify via
        /// <see cref="GetBucketEncryptionStatusAsync"/>.
        /// </summary>
        public async Task EnsureBucketEncryptionAsync(
            string bucketName,
            BucketEncryptionConfig config,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));

            config ??= new BucketEncryptionConfig();

            if (config.Algorithm == BucketEncryptionAlgorithm.SseKms && string.IsNullOrWhiteSpace(config.KmsKeyId))
                throw new ArgumentException("KmsKeyId is required when Algorithm is SSE-KMS.", nameof(config));

            try
            {
                var request = new PutBucketEncryptionRequest
                {
                    BucketName = bucketName,
                    ServerSideEncryptionConfiguration = new ServerSideEncryptionConfiguration
                    {
                        ServerSideEncryptionRules = new List<ServerSideEncryptionRule>
                        {
                            new ServerSideEncryptionRule
                            {
                                ServerSideEncryptionByDefault = new ServerSideEncryptionByDefault
                                {
                                    ServerSideEncryptionAlgorithm = config.Algorithm == BucketEncryptionAlgorithm.SseKms
                                        ? ServerSideEncryptionMethod.AWSKMS
                                        : ServerSideEncryptionMethod.AES256,
                                    ServerSideEncryptionKeyManagementServiceKeyId =
                                        config.Algorithm == BucketEncryptionAlgorithm.SseKms
                                            ? config.KmsKeyId
                                            : null
                                }
                            }
                        }
                    }
                };

                await _s3Client.PutBucketEncryptionAsync(request, cancellationToken);

                // Re-verify fail-closed: encryption must confirm as Enabled after apply.
                var status = await GetBucketEncryptionStatusAsync(bucketName, cancellationToken);
                var expectedAlgorithm = config.Algorithm == BucketEncryptionAlgorithm.SseKms
                    ? ServerSideEncryptionMethod.AWSKMS.Value
                    : ServerSideEncryptionMethod.AES256.Value;
                if (!status.IsEncryptionVerified ||
                    !string.Equals(status.SseAlgorithm, expectedAlgorithm, StringComparison.Ordinal) ||
                    (config.Algorithm == BucketEncryptionAlgorithm.SseKms &&
                     !string.Equals(status.KmsKeyId, config.KmsKeyId, StringComparison.Ordinal)))
                {
                    throw new OEliteException(
                        $"Bucket encryption enforcement failed for bucket '{bucketName}': " +
                        $"backend reports {status.Status} after PutBucketEncryption; " +
                        $"requested algorithm={expectedAlgorithm}, observed algorithm={status.SseAlgorithm ?? "none"}. " +
                        $"Evidence: {status.Evidence}");
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw new OEliteException(
                    $"Bucket encryption enforcement failed for bucket '{bucketName}': " +
                    $"HTTP {(int)ex.StatusCode} {ex.StatusCode}, error code {ex.ErrorCode ?? "none"}", ex);
            }
        }

        /// <summary>
        /// Ensure Object Lock is enabled on the bucket with the given retention mode
        /// and retention period (days).
        /// </summary>
        public async Task EnsureObjectLockEnabledAsync(
            string bucketName,
            ObjectLockMode mode,
            int retentionDays,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentException("Bucket name cannot be null or empty.", nameof(bucketName));
            if (retentionDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention period must be greater than zero.");

            string modeValue = mode == ObjectLockMode.Compliance ? "COMPLIANCE" : "GOVERNANCE";

            try
            {
                var existing = await _s3Client.GetObjectLockConfigurationAsync(
                    new GetObjectLockConfigurationRequest { BucketName = bucketName },
                    cancellationToken);

                bool lockEnabled = existing.ObjectLockConfiguration?.ObjectLockEnabled == "Enabled";
                if (!lockEnabled)
                {
                    throw new OEliteException(
                        $"Object Lock is not enabled for bucket '{bucketName}'. Object Lock cannot be enabled " +
                        $"on an existing bucket by backends such as MinIO — create the bucket with Object Lock enabled.");
                }

                // Lock is already enabled: apply/adjust default retention.
                await _s3Client.PutObjectLockConfigurationAsync(
                    new PutObjectLockConfigurationRequest
                    {
                        BucketName = bucketName,
                        ObjectLockConfiguration = new ObjectLockConfiguration
                        {
                            ObjectLockEnabled = "Enabled",
                            Rule = new ObjectLockRule
                            {
                                DefaultRetention = new DefaultRetention
                                {
                                    Mode = modeValue,
                                    Days = retentionDays
                                }
                            }
                        }
                    },
                    cancellationToken);
                return;
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                (ex.ErrorCode ?? string.Empty).Contains("NoSuchObjectLockConfiguration", StringComparison.OrdinalIgnoreCase))
            {
                // Object Lock configuration absent. The bucket may exist without Object Lock
                // (MinIO: lock must be enabled at creation) or may not exist at all.
                var bucketReachable = await BucketExistsAsync(bucketName, cancellationToken);
                if (!bucketReachable)
                {
                    // Fresh bucket: create with Object Lock enabled at creation time
                    // (MinIO and AWS both reject enabling Object Lock on existing buckets),
                    // then apply the default retention rule.
                    await _s3Client.PutBucketAsync(
                        new PutBucketRequest
                        {
                            BucketName = bucketName,
                            ObjectLockEnabledForBucket = true
                        }, cancellationToken);

                    try
                    {
                        await _s3Client.PutObjectLockConfigurationAsync(
                            new PutObjectLockConfigurationRequest
                            {
                                BucketName = bucketName,
                                ObjectLockConfiguration = new ObjectLockConfiguration
                                {
                                    ObjectLockEnabled = "Enabled",
                                    Rule = new ObjectLockRule
                                    {
                                        DefaultRetention = new DefaultRetention
                                        {
                                            Mode = modeValue,
                                            Days = retentionDays
                                        }
                                    }
                                }
                            },
                            cancellationToken);
                    }
                    catch (AmazonS3Exception ex2)
                    {
                        throw new OEliteException(
                            $"Object Lock enforcement failed for bucket '{bucketName}': bucket created without Object Lock " +
                            $"and the backend cannot enable Object Lock on an existing bucket. " +
                            $"Create the bucket with Object Lock enabled instead. HTTP {(int)ex2.StatusCode} {ex2.StatusCode}, " +
                            $"error code {ex2.ErrorCode ?? "none"}", ex2);
                    }
                    return;
                }

                // Bucket exists without Object Lock: backends such as MinIO cannot enable it post-creation.
                throw new OEliteException(
                    $"Object Lock is not enabled for bucket '{bucketName}' and the backend cannot enable Object Lock " +
                    $"on an existing bucket. Create the bucket with Object Lock enabled.");
            }
            catch (AmazonS3Exception ex) when (ClassificationIndicatesUnavailable(ex))
            {
                throw new OEliteException(
                    $"Object Lock enforcement failed for bucket '{bucketName}': backend does not support Object Lock. " +
                    $"HTTP {(int)ex.StatusCode} {ex.StatusCode}, error code {ex.ErrorCode ?? "none"}", ex);
            }
        }

        /// <summary>
        /// AWS-SDK error classification mirrored from the origin-auth capability probe so
        /// the two surfaces agree on backend capability evidence (restme#9 / origin-auth#1416).
        /// </summary>
        internal static BucketEncryptionStatus ClassifyS3Error(AmazonS3Exception ex)
        {
            // Verified-encryption semantics: an error can never confirm encryption.
            if (ex.StatusCode == HttpStatusCode.NotImplemented)
                return BucketEncryptionStatus.Unavailable;

            var errorCode = ex.ErrorCode ?? string.Empty;
            if (errorCode.Contains("ServerSideEncryptionConfigurationNotFound", StringComparison.OrdinalIgnoreCase))
                return BucketEncryptionStatus.Disabled; // bucket reachable, no default encryption

            if (errorCode.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("NotSupported", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("KMS", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("NotInitialized", StringComparison.OrdinalIgnoreCase) ||
                errorCode.Contains("MethodNotAllowed", StringComparison.OrdinalIgnoreCase))
                return BucketEncryptionStatus.Unavailable;

            if (ex.StatusCode == HttpStatusCode.NotFound ||
                errorCode.Contains("NoSuchBucket", StringComparison.OrdinalIgnoreCase))
                return BucketEncryptionStatus.Unavailable; // bucket missing: nothing to verify

            return BucketEncryptionStatus.Unavailable;
        }

        private static bool ClassificationIndicatesUnavailable(AmazonS3Exception ex) =>
            ex.StatusCode == HttpStatusCode.NotImplemented ||
            ex.StatusCode == HttpStatusCode.MethodNotAllowed ||
            ex.ErrorCode.Contains("NotImplemented", StringComparison.OrdinalIgnoreCase) ||
            ex.ErrorCode.Contains("NotSupported", StringComparison.OrdinalIgnoreCase) ||
            ex.ErrorCode.Contains("MethodNotAllowed", StringComparison.OrdinalIgnoreCase);

        private async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken)
        {
            try
            {
                await _s3Client.GetBucketLocationAsync(
                    new GetBucketLocationRequest { BucketName = bucketName }, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }
    }
}
