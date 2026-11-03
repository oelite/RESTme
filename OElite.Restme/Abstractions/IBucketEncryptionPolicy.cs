using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Server-side encryption algorithm used for bucket default encryption.
    /// </summary>
    public enum BucketEncryptionAlgorithm
    {
        /// <summary>SSE-S3 (AES256) — keys managed by the storage backend.</summary>
        SseS3,

        /// <summary>SSE-KMS (aws:kms) — requires a KMS key id.</summary>
        SseKms
    }

    /// <summary>
    /// Object Lock retention mode.
    /// </summary>
    public enum ObjectLockMode
    {
        /// <summary>Governance mode — most users can override; compliance-sensitive actions blocked.</summary>
        Governance,

        /// <summary>Compliance mode — no user, including root, can override until retention expires.</summary>
        Compliance
    }

    /// <summary>
    /// Configuration for applying default bucket encryption via
    /// <see cref="IBucketEncryptionPolicy.EnsureBucketEncryptionAsync"/>.
    /// </summary>
    public sealed class BucketEncryptionConfig
    {
        /// <summary>Default is SSE-S3.</summary>
        public BucketEncryptionAlgorithm Algorithm { get; set; } = BucketEncryptionAlgorithm.SseS3;

        /// <summary>
        /// KMS key id used when <see cref="Algorithm"/> is <see cref="BucketEncryptionAlgorithm.SseKms"/>.
        /// Required for SSE-KMS; ignored for SSE-S3.
        /// </summary>
        public string? KmsKeyId { get; set; }
    }

    /// <summary>
    /// Status of a bucket-encryption capability probe against the storage backend.
    /// </summary>
    public enum BucketEncryptionStatus
    {
        /// <summary>Default bucket encryption confirmed via the backend API (GetBucketEncryption returned at least one rule).</summary>
        Enabled,

        /// <summary>Bucket reachable but no default encryption configured (ServerSideEncryptionConfigurationNotFoundError).</summary>
        Disabled,

        /// <summary>Backend cannot serve SSE at all, or the endpoint could not be reached to verify.
        /// Explicit first-class status so callers can distinguish "not verified" from "backend cannot do SSE".</summary>
        Unavailable
    }

    /// <summary>
    /// Evidence-bearing, log-safe result of a bucket-encryption status probe.
    /// Never contains credentials or connection details.
    /// </summary>
    public sealed record BucketEncryptionStatusResult(
        BucketEncryptionStatus Status,
        string? SseAlgorithm,
        string? KmsKeyId,
        string Evidence,
        DateTimeOffset ProbedAtUtc)
    {
        /// <summary>True only when encryption was verified against the storage backend's own API.</summary>
        public bool IsEncryptionVerified => Status == BucketEncryptionStatus.Enabled;
    }

    /// <summary>
    /// Verifiable bucket-encryption capability contract (oelite/uranus/restme#9).
    /// Enables downstream services (origin-auth first) to enforce server-side-encryption
    /// fail-closed storage without config-flag attestation or ad-hoc bucket mutations.
    /// </summary>
    /// <remarks>
    /// Read operations (<see cref="GetBucketEncryptionStatusAsync"/>) never throw for
    /// unsupported backends — they return <see cref="BucketEncryptionStatus.Unavailable"/>
    /// with real backend error evidence instead.
    /// Write operations (<see cref="EnsureBucketEncryptionAsync"/>,
    /// <see cref="EnsureObjectLockEnabledAsync"/>) fail closed: they throw descriptive
    /// exceptions when the backend cannot serve the requested capability, so SSE can
    /// never be silently assumed.
    /// </remarks>
    public interface IBucketEncryptionPolicy
    {
        /// <summary>
        /// Query the storage backend for the bucket's default encryption status.
        /// Returns <see cref="BucketEncryptionStatus.Enabled"/>,
        /// <see cref="BucketEncryptionStatus.Disabled"/> or
        /// <see cref="BucketEncryptionStatus.Unavailable"/> with evidence from the S3 API response.
        /// Never throws for unsupported backends.
        /// </summary>
        Task<BucketEncryptionStatusResult> GetBucketEncryptionStatusAsync(
            string bucketName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Apply default bucket encryption (SSE-S3 or SSE-KMS) and re-verify via
        /// <see cref="GetBucketEncryptionStatusAsync"/>.
        /// Throws a descriptive exception when the backend cannot serve the requested
        /// encryption or when post-apply verification does not confirm Enabled (fail-closed).
        /// </summary>
        Task EnsureBucketEncryptionAsync(
            string bucketName,
            BucketEncryptionConfig config,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensure Object Lock is enabled on the bucket with the given retention mode and
        /// retention period (days). Buckets created without Object Lock cannot be retrofitted
        /// by backends such as MinIO — such backends fail closed with a descriptive exception.
        /// </summary>
        Task EnsureObjectLockEnabledAsync(
            string bucketName,
            ObjectLockMode mode,
            int retentionDays,
            CancellationToken cancellationToken = default);
    }
}
