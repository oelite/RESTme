using System;
using System.Threading.Tasks;
using OElite.Providers;
using Testcontainers.Minio;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace OElite.Restme.S3.IntegrationTests;

/// <summary>
/// Base class for S3 integration tests using MinIO test containers
/// Enterprise-grade testing infrastructure with comprehensive setup and teardown
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected RestConfig Config { get; private set; } = null!;
    protected IRestme Rest { get; private set; }

    // Use AWS-compatible credentials for MinIO
    private const string MinioAccessKey = "AKIAIOSFODNN7EXAMPLE";
    private const string MinioSecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private readonly MinioContainer _minioContainer = new MinioBuilder()
        .WithImage("minio/minio:latest") // Use Docker Hub registry
        .WithPortBinding(9000, true)
        .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilPortIsAvailable(9000)) // Wait for port instead of log message
        .Build();

    // Connection properties
    protected bool IsContainerRunning => _minioContainer?.State == TestcontainersStates.Running;

    public virtual async Task InitializeAsync()
    {
        await _minioContainer.StartAsync();

        // Use connection string approach which includes proper S3-compatible settings
        var port = _minioContainer.GetMappedPublicPort(9000);
        Config = new RestConfig(RestMode.S3)
        {
            ConnectionString = $"endpoint=http://localhost:{port};bucket=test-bucket;accesskey={MinioAccessKey};secretkey={MinioSecretKey};forcepathstyle=true;usehttp=true",
            InstanceName = "test-bucket",
            AuthKey = MinioAccessKey, // AWS-compatible 20-char access key (backup)
            AuthSecret = MinioSecretKey // AWS-compatible 40-char secret key (backup)
        };

        Rest = new Rest(Config);

        // Wait for MinIO to be fully ready
        await Task.Delay(3000);

        // // Use our StorageProvider to verify connectivity by attempting a simple operation
        // var testKey = "connection-test";
        // try
        // {
        //     await StorageProvider.PutStringAsync(testKey, "test");
        //     await StorageProvider.DeleteAsync(testKey);
        //     Console.WriteLine("✅ S3StorageProvider connection successful");
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"❌ S3StorageProvider connection failed: {ex.Message}");
        //     throw new InvalidOperationException($"Failed to establish connection with S3StorageProvider: {ex.Message}",
        //         ex);
        // }
    }

    public virtual async Task DisposeAsync()
    {
        Rest?.Dispose();
        await _minioContainer.DisposeAsync();
    }
}