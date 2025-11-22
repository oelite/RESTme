using System.Text;
using OElite.Providers;
using Testcontainers.Minio;
using DotNet.Testcontainers.Builders;
using Xunit;

namespace OElite.Restme.S3.IntegrationTests;

public class S3IntegrationTests : IAsyncLifetime
{
    private readonly MinioContainer _minioContainer = new MinioBuilder()
        .WithImage("minio/minio:RELEASE.2024-01-16T16-07-38Z")
        .WithPortBinding(9000, true)
        .WithPortBinding(9001, true)
        .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
        .WithCommand("server", "/data", "--console-address", ":9001")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
        .Build();

    private RestConfig? _config;
    private S3StorageProvider? _storageProvider;

    public async Task InitializeAsync()
    {
        await _minioContainer.StartAsync();

        _config = new RestConfig
        {
            ConnectionString = $"s3://localhost:{_minioContainer.GetMappedPublicPort(9000)}/test-bucket",
            AuthKey = "minioadmin",
            AuthSecret = "minioadmin"
        };

        _storageProvider = new S3StorageProvider(_config);
    }

    public async Task DisposeAsync()
    {
        await _minioContainer.DisposeAsync();
    }

    [Fact]
    public void S3StorageProvider_ShouldImplementIRestmeProvider()
    {
        // Arrange & Act
        var provider = _storageProvider;

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("S3Storage", provider.ProviderName);
        Assert.Equal(ProviderCapabilities.Storage, provider.Capabilities);
        Assert.NotNull(provider.Configuration);
    }

    [Fact]
    public async Task PutStringAsync_ShouldStoreDataSuccessfully()
    {
        // Arrange
        var key = $"test-file-{Guid.NewGuid()}.txt";
        var content = "Test content for S3 storage operation";

        // Act
        var result = await _storageProvider!.PutStringAsync(key, content);

        // Assert
        Assert.Equal(content, result);
        var exists = await _storageProvider!.ExistsAsync(key);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStringAsync_ShouldRetrieveDataSuccessfully()
    {
        // Arrange
        var key = $"retrieve-test-{Guid.NewGuid()}.txt";
        var originalContent = "Content to retrieve from S3";
        await _storageProvider!.PutStringAsync(key, originalContent);

        // Act
        var retrievedContent = await _storageProvider!.GetStringAsync(key);

        // Assert
        Assert.Equal(originalContent, retrievedContent);
    }

    [Fact]
    public async Task PutStreamAsync_ShouldStoreBinaryDataSuccessfully()
    {
        // Arrange
        var key = $"binary-test-{Guid.NewGuid()}.bin";
        var content = "Binary content for stream operations";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _storageProvider!.PutStreamAsync(key, stream);

        // Assert
        Assert.True(result);
        var exists = await _storageProvider!.ExistsAsync(key);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStreamAsync_ShouldRetrieveBinaryDataSuccessfully()
    {
        // Arrange
        var key = $"stream-test-{Guid.NewGuid()}.bin";
        var originalContent = "Stream content for binary operations";
        var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        await _storageProvider!.PutStreamAsync(key, uploadStream);

        // Act
        var retrievedStream = await _storageProvider!.GetStreamAsync(key);
        Assert.NotNull(retrievedStream);
        var retrievedContent = new StreamReader(retrievedStream!).ReadToEnd();

        // Assert
        Assert.Equal(originalContent, retrievedContent);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDataSuccessfully()
    {
        // Arrange
        var key = $"delete-test-{Guid.NewGuid()}.txt";
        var content = "Content to be deleted";
        await _storageProvider!.PutStringAsync(key, content);

        // Verify exists before deletion
        var existsBefore = await _storageProvider!.ExistsAsync(key);
        Assert.True(existsBefore);

        // Act
        var deleteResult = await _storageProvider!.DeleteAsync(key);

        // Assert
        Assert.True(deleteResult);
        var existsAfter = await _storageProvider!.ExistsAsync(key);
        Assert.False(existsAfter);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingFile()
    {
        // Arrange
        var key = $"exists-test-{Guid.NewGuid()}.txt";
        var content = "File that exists";
        await _storageProvider!.PutStringAsync(key, content);

        // Act
        var exists = await _storageProvider!.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseForNonExistingFile()
    {
        // Arrange
        var key = $"non-existing-{Guid.NewGuid()}.txt";

        // Act
        var exists = await _storageProvider!.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task MultipleOperations_ShouldWorkConcurrently()
    {
        // Arrange
        var tasks = new List<Task>();
        var keys = new List<string>();

        // Create multiple concurrent operations
        for (int i = 0; i < 5; i++)
        {
            var key = $"concurrent-test-{Guid.NewGuid()}-{i}.txt";
            keys.Add(key);

            tasks.Add(Task.Run(async () =>
            {
                var content = $"Concurrent content {Guid.NewGuid()}";
                await _storageProvider!.PutStringAsync(key, content);
                var retrieved = await _storageProvider!.GetStringAsync(key);
                Assert.Equal(content, retrieved);
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All operations completed successfully
        foreach (var key in keys)
        {
            var exists = await _storageProvider!.ExistsAsync(key);
            Assert.True(exists);
        }
    }
}