using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using OElite.Providers;
using OElite.Restme;
using OElite.Restme.Abstractions;
using Xunit;
using ObjectLockMode = OElite.Restme.Abstractions.ObjectLockMode;

namespace OElite.Restme.S3.IntegrationTests;

[Trait("Category", "Integration")]
[Trait("Category", "SkipCI")]
public sealed class S3BucketEncryptionPolicyIntegrationTests
{
    private static string AccessKey => Environment.GetEnvironmentVariable("MINIO_ROOT_USER") ?? "oelite";
    private static string SecretKey => Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD")
        ?? throw new InvalidOperationException("MINIO_ROOT_PASSWORD is required from the shared oelite-stack .env.");

    private static string Bucket() => $"restme-enc-test-{Guid.NewGuid():N}";

    private static RestConfig Config(string bucket) => new(RestMode.S3)
    {
        ConnectionString = $"endpoint=http://localhost:9000;bucket={bucket};accesskey={AccessKey};secretkey={SecretKey};forcepathstyle=true;usehttp=true",
        AuthKey = AccessKey,
        AuthSecret = SecretKey,
        InstanceName = bucket
    };

    private static S3BucketEncryptionPolicy Policy(string bucket) => new(Config(bucket));
    private static IAmazonS3 Client(string bucket) => S3BucketEncryptionPolicy.CreateAmazonClient(Config(bucket));

    [Fact]
    public async Task GetStatus_WhenSharedStackMinioRunning_ReturnsExplicitStatusWithEvidence()
    {
        var bucket = Bucket();
        using var client = Client(bucket);
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        try
        {
            var result = await Policy(bucket).GetBucketEncryptionStatusAsync(bucket);
            Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
            Assert.True(result.ProbedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(5));
            Assert.True(result.Status is BucketEncryptionStatus.Disabled or BucketEncryptionStatus.Unavailable);
            Assert.False(result.IsEncryptionVerified);
        }
        finally
        {
            await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucket });
        }
    }

    [Fact]
    public async Task GetStatus_WhenBucketMissing_ReturnsUnverifiedStatusNotException()
    {
        var bucket = Bucket();
        var result = await Policy(bucket).GetBucketEncryptionStatusAsync(bucket);
        Assert.NotEqual(BucketEncryptionStatus.Enabled, result.Status);
        Assert.False(result.IsEncryptionVerified);
        Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
    }

    [Fact]
    public async Task EnsureBucketEncryption_WhenBackendCannotServeSse_FailsClosedWithDescriptiveException()
    {
        var bucket = Bucket();
        using var client = Client(bucket);
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        try
        {
            var policy = Policy(bucket);
            var ex = await Assert.ThrowsAsync<OEliteException>(() => policy.EnsureBucketEncryptionAsync(bucket,
                new BucketEncryptionConfig { Algorithm = BucketEncryptionAlgorithm.SseS3 }));
            Assert.Contains(bucket, ex.Message);
            Assert.Contains("NotImplemented", ex.Message, StringComparison.OrdinalIgnoreCase);
            var status = await policy.GetBucketEncryptionStatusAsync(bucket);
            Assert.False(status.IsEncryptionVerified);
        }
        finally
        {
            await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucket });
        }
    }

    [Fact]
    public async Task EnsureObjectLock_WhenBackendCannotServeLock_FailsClosedWithDescriptiveException()
    {
        var bucket = Bucket();
        using var client = Client(bucket);
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        try
        {
            var ex = await Assert.ThrowsAsync<OEliteException>(() =>
                Policy(bucket).EnsureObjectLockEnabledAsync(bucket, ObjectLockMode.Governance, 30));
            Assert.Contains("Object Lock", ex.Message);
        }
        finally
        {
            await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucket });
        }
    }

    [Fact]
    public async Task EnsureObjectLock_WhenBucketMissing_CreatesBucketWithLockAndAppliesRetention()
    {
        var bucket = Bucket();
        using var client = Client(bucket);
        try
        {
            await Policy(bucket).EnsureObjectLockEnabledAsync(bucket, ObjectLockMode.Compliance, 7);
            await client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucket });
            var lockConfig = await client.GetObjectLockConfigurationAsync(
                new GetObjectLockConfigurationRequest { BucketName = bucket });
            Assert.Equal("Enabled", lockConfig.ObjectLockConfiguration?.ObjectLockEnabled);
            Assert.Equal("COMPLIANCE", lockConfig.ObjectLockConfiguration?.Rule?.DefaultRetention?.Mode);
            Assert.Equal(7, lockConfig.ObjectLockConfiguration?.Rule?.DefaultRetention?.Days);
        }
        finally
        {
            try { await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucket }); }
            catch (AmazonS3Exception) { /* retained objects can prevent test cleanup */ }
        }
    }

    [Fact]
    public async Task GetStatus_WhenSecondaryProbeDenied_ReturnsUnavailableWithSafeEvidence()
    {
        var seam = DispatchProxy.Create<IAmazonS3, S3ErrorProxy>();
        var result = await new S3BucketEncryptionPolicy(seam).GetBucketEncryptionStatusAsync(Bucket());
        Assert.Equal(BucketEncryptionStatus.Unavailable, result.Status);
        Assert.False(result.IsEncryptionVerified);
        Assert.Contains("AccessDenied", result.Evidence);
        Assert.DoesNotContain("secret", result.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassificationIndicatesUnavailable_WhenErrorCodeNull_DoesNotThrow()
    {
        var classifier = typeof(S3BucketEncryptionPolicy).GetMethod("ClassificationIndicatesUnavailable",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(classifier);
        var error = new AmazonS3Exception("unavailable") { StatusCode = HttpStatusCode.BadRequest };
        Assert.False((bool)classifier.Invoke(null, new object[] { error })!);
    }

    public class S3ErrorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "GetBucketEncryptionAsync")
                return Task.FromException<GetBucketEncryptionResponse>(new AmazonS3Exception("configuration absent")
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorCode = "ServerSideEncryptionConfigurationNotFound"
                });
            if (targetMethod?.Name == "GetBucketLocationAsync")
                return Task.FromException<GetBucketLocationResponse>(new AmazonS3Exception("denied")
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorCode = "AccessDenied"
                });
            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
