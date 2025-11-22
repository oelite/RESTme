using System.Text;
using OElite.Providers;
using OElite.Restme.Abstractions;
using Xunit;

namespace OElite.Restme.S3.IntegrationTests;

public class S3IntegrationTests : TestBase
{
    protected IStorageProvider StorageProvider { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        StorageProvider = Rest.GetProvider<IStorageProvider>()!;
    }

    [Fact]
    public void S3StorageProvider_ShouldImplementIRestmeProvider()
    {
        // Arrange & Act
        var provider = StorageProvider;

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
        var result = await StorageProvider!.PutStringAsync(key, content);

        // Assert
        Assert.Equal(content, result);
        var exists = await StorageProvider!.ExistsAsync(key);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStringAsync_ShouldRetrieveDataSuccessfully()
    {
        // Arrange
        var key = $"retrieve-test-{Guid.NewGuid()}.txt";
        var originalContent = "Content to retrieve from S3";
        await StorageProvider!.PutStringAsync(key, originalContent);

        // Act
        var retrievedContent = await StorageProvider!.GetStringAsync(key);

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
        var result = await StorageProvider!.PutStreamAsync(key, stream);

        // Assert
        Assert.True(result);
        var exists = await StorageProvider!.ExistsAsync(key);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStreamAsync_ShouldRetrieveBinaryDataSuccessfully()
    {
        // Arrange
        var key = $"stream-test-{Guid.NewGuid()}.bin";
        var originalContent = "Stream content for binary operations";
        var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        await StorageProvider!.PutStreamAsync(key, uploadStream);

        // Act
        var retrievedStream = await StorageProvider!.GetStreamAsync(key);
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
        await StorageProvider!.PutStringAsync(key, content);

        // Verify exists before deletion
        var existsBefore = await StorageProvider!.ExistsAsync(key);
        Assert.True(existsBefore);

        // Act
        var deleteResult = await StorageProvider!.DeleteAsync(key);

        // Assert
        Assert.True(deleteResult);
        var existsAfter = await StorageProvider!.ExistsAsync(key);
        Assert.False(existsAfter);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingFile()
    {
        // Arrange
        var key = $"exists-test-{Guid.NewGuid()}.txt";
        var content = "File that exists";
        await StorageProvider!.PutStringAsync(key, content);

        // Act
        var exists = await StorageProvider!.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseForNonExistingFile()
    {
        // Arrange
        var key = $"non-existing-{Guid.NewGuid()}.txt";

        // Act
        var exists = await StorageProvider!.ExistsAsync(key);

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
                await StorageProvider!.PutStringAsync(key, content);
                var retrieved = await StorageProvider!.GetStringAsync(key);
                Assert.Equal(content, retrieved);
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All operations completed successfully
        foreach (var key in keys)
        {
            var exists = await StorageProvider!.ExistsAsync(key);
            Assert.True(exists);
        }
    }
}