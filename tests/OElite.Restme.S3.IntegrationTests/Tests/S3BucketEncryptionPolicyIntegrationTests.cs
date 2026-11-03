using System;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging.Abstractions;
using OElite.Providers;
using OElite.Restme;
using OElite.Restme.Abstractions;
using Xunit;
using ObjectLockMode = OElite.Restme.Abstractions.ObjectLockMode;

namespace OElite.Restme.S3.IntegrationTests;

/// <summary>
///     Integration tests for the verifiable bucket-encryption capability contract
///     (oelite/uranus/restme#9) against the shared-stack MinIO
///     (coding-standards/infrastructure/oelite-stack — MinIO on localhost:9000).
///     <para>
///     Verifies against a REAL backend:
///     1) GetBucketEncryptionStatusAsync returns explicit Enabled/Disabled/Unavailable
///        with real backend evidence — never an exception for unsupported backends;
///     2) EnsureBucketEncryptionAsync applies default encryption and re-verifies
///        fail-closed when the backend supports SSE;
///     3) EnsureObjectLockEnabledAsync fails closed with a descriptive exception when
///        the backend cannot serve Object Lock.
///     </para>
///     The shared-stack MinIO has no KES/KMS. Depending on MinIO version the backend
///     answers 404 ServerSideEncryptionConfigurationNotFound (Disabled) or
///     501 NotImplemented (Unavailable) — either way verification never claims Enabled
///     unless the backend's own API confirms it.
///     <para>
///     Excluded from CI per the shared-infrastructure testing policy; run locally:
///     ./oelite-stack.sh up, then MINIO_ROOT_PASSWORD=... dotnet test --filter Category=Integration
///     </para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "SkipCI")]
public sealed class S3BucketEncryptionPolicyIntegrationTests
{
    private const string BucketName = "restme-enc-contract-test";

    private static string AccessKey =>
        Environment.GetEnvironmentVariable("MINIO_ROOT_USER") ?? "oelite";

    private static string SecretKey =>
        Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD")
        ?? throw new InvalidOperationException(
            "MINIO_ROOT_PASSWORD is required: source the oelite-stack .env before running integration tests.");

    private static RestConfig BuildRestConfig(string bucketName) =>
        new RestConfig(RestMode.S3)
        {
            ConnectionString =
                $"endpoint=http://localhost:9000;bucket={bucketName};accesskey={AccessKey};secretkey={SecretKey};forcepathstyle=true;usehttp=true",
            AuthKey = AccessKey,
            AuthSecret = SecretKey,
            InstanceName = bucketName
        };

    private static S3BucketEncryptionPolicy BuildPolicy() =>
        new S3BucketEncryptionPolicy(BuildRestConfig(BucketName));

    private static IAmazonS3 BuildClient() => S3BucketEncryptionPolicy.CreateAmazonClient(BuildRestConfig(BucketName));

    private static async Task<bool> BucketExistsAsync(IAmazonS3 client, string bucketName)
    {
        try
        {
            await client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucketName });
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [Fact]
    public async Task GetStatus_WhenSharedStackMinioRunning_ReturnsExplicitStatusWithEvidence()
    {
        var policy = BuildPolicy();

        var result = await policy.GetBucketEncryptionStatusAsync(BucketName);

        // Real backend evidence must always be present — no exception, no faked states.
        Assert.False(string.IsNullOrWhiteSpace(result.Evidence),
            "probe must carry real backend evidence, not an empty string");
        Assert.True(result.ProbedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(5));

        // Shared-stack MinIO has no KMS: the honest result is Disabled or Unavailable —
        // never Enabled unless the backend's own API confirms default encryption.
        Assert.True(result.Status is BucketEncryptionStatus.Disabled or BucketEncryptionStatus.Unavailable,
            $"expected Disabled or Unavailable against shared-stack MinIO, got {result.Status}: {result.Evidence}");
        Assert.False(result.IsEncryptionVerified,
            "shared-stack MinIO has no KMS; verification must never claim Enabled");
    }

    [Fact]
    public async Task GetStatus_WhenBucketMissing_ReturnsUnverifiedStatusNotException()
    {
        var policy = BuildPolicy();
        var missingBucket = $"restme-enc-missing-{Guid.NewGuid():N}";

        var result = await policy.GetBucketEncryptionStatusAsync(missingBucket);

        // No capability method may throw for unsupported/missing backends. Real
        // shared-stack MinIO answers ServerSideEncryptionConfigurationNotFound for a
        // missing bucket (Disabled); AWS answers NoSuchBucket (Unavailable). Either
        // way the result is explicitly unverified with backend evidence.
        Assert.NotEqual(BucketEncryptionStatus.Enabled, result.Status);
        Assert.False(result.IsEncryptionVerified);
        Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
    }

    [Fact]
    public async Task EnsureBucketEncryption_WhenBackendCannotServeSse_FailsClosedWithDescriptiveException()
    {
        var client = BuildClient();
        try
        {
            var policy = BuildPolicy();

            // Real shared-stack MinIO (no KES/KMS) answers 501 NotImplemented
            // ("Server side encryption specified but KMS is not configured") for
            // PutBucketEncryption — even for SSE-S3. The enforce path must fail
            // closed with a descriptive exception carrying backend evidence:
            // never a silent no-op and never a false success.
            var ex = await Assert.ThrowsAsync<OEliteException>(
                () => policy.EnsureBucketEncryptionAsync(BucketName,
                    new BucketEncryptionConfig { Algorithm = BucketEncryptionAlgorithm.SseS3 }));

            Assert.Contains("restme-enc-contract-test", ex.Message);
            Assert.Contains("NotImplemented", ex.Message, StringComparison.OrdinalIgnoreCase);

            // The status probe after the failed enforce must still be explicitly unverified.
            var status = await policy.GetBucketEncryptionStatusAsync(BucketName);
            Assert.False(status.IsEncryptionVerified);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task EnsureObjectLock_WhenBackendCannotServeLock_FailsClosedWithDescriptiveException()
    {
        var client = BuildClient();
        try
        {
            var policy = BuildPolicy();

            // The test bucket was created without Object Lock (shared-stack MinIO cannot
            // enable lock on an existing bucket). Enforce must fail closed — a descriptive
            // exception, never a silent no-op or a false success.
            var ex = await Assert.ThrowsAsync<OEliteException>(
                () => policy.EnsureObjectLockEnabledAsync(BucketName, ObjectLockMode.Governance, 30));

            Assert.Contains("Object Lock", ex.Message);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task EnsureObjectLock_WhenBucketMissing_CreatesBucketWithLockAndAppliesRetention()
    {
        var client = BuildClient();
        var freshBucket = $"restme-enc-lock-{Guid.NewGuid():N}";
        try
        {
            var policy = new S3BucketEncryptionPolicy(BuildRestConfig(freshBucket));

            // Missing bucket: the enforce path creates it and enables Object Lock.
            await policy.EnsureObjectLockEnabledAsync(freshBucket, ObjectLockMode.Compliance, 7);

            var exists = await BucketExistsAsync(client, freshBucket);
            Assert.True(exists, "bucket must be created by the enforce path when missing");

            // Retention config must be readable back from the backend.
            var lockConfig = await client.GetObjectLockConfigurationAsync(
                new GetObjectLockConfigurationRequest { BucketName = freshBucket });
            Assert.Equal("Enabled", lockConfig.ObjectLockConfiguration?.ObjectLockEnabled);
            Assert.Equal("COMPLIANCE",
                lockConfig.ObjectLockConfiguration?.Rule?.DefaultRetention?.Mode);
            Assert.Equal(7, lockConfig.ObjectLockConfiguration?.Rule?.DefaultRetention?.Days);
        }
        finally
        {
            // Cleanup: remove the created test bucket (force, incl. lock config).
            try { await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = freshBucket }); }
            catch { /* best-effort cleanup */ }
            client.Dispose();
        }
    }
}
