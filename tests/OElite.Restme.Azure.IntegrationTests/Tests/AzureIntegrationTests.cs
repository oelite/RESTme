using System.Text;
using OElite.Providers;
using OElite.Restme.Abstractions;
using Testcontainers.Azurite;
using DotNet.Testcontainers.Builders;
using Xunit;

namespace OElite.Restme.Azure.IntegrationTests;

public class AzureIntegrationTests : TestBase
{
    private IStorageProvider? _storageProvider;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _storageProvider = Restme.GetProvider<IStorageProvider>();
    }

    public new async Task DisposeAsync()
    {
        _storageProvider?.Dispose();
        await base.DisposeAsync();
    }

    [Fact]
    public void AzureStorageProvider_ShouldImplementIRestmeProvider()
    {
        // Arrange & Act
        var provider = _storageProvider;

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("AzureStorage", provider.ProviderName);
        Assert.Equal(ProviderCapabilities.Storage, provider.Capabilities);
        Assert.NotNull(provider.Configuration);
    }

    [Fact]
    public async Task PutStringAsync_ShouldStoreBlobSuccessfully()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/test-blob.txt";
        var content = "Test content for Azure blob storage";

        // Act
        var result = await _storageProvider!.PutStringAsync(blobName, content);

        // Assert
        Assert.Equal(content, result);
        var exists = await _storageProvider.ExistsAsync(blobName);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStringAsync_ShouldRetrieveBlobSuccessfully()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/retrieve-blob.txt";
        var originalContent = "Content to retrieve from Azure blob";
        await _storageProvider!.PutStringAsync(blobName, originalContent);

        // Act
        var retrievedContent = await _storageProvider.GetStringAsync(blobName);

        // Assert
        Assert.Equal(originalContent, retrievedContent);
    }

    [Fact]
    public async Task PutStreamAsync_ShouldStoreBinaryBlobSuccessfully()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/binary-blob.bin";
        var content = "Binary content for Azure stream operations";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _storageProvider!.PutStreamAsync(blobName, stream);

        // Assert
        Assert.True(result);
        var exists = await _storageProvider.ExistsAsync(blobName);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetStreamAsync_ShouldRetrieveBinaryBlobSuccessfully()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/stream-blob.bin";
        var originalContent = "Stream content for Azure binary operations";
        var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(originalContent));
        await _storageProvider!.PutStreamAsync(blobName, uploadStream);

        // Act
        var retrievedStream = await _storageProvider.GetStreamAsync(blobName);
        Assert.NotNull(retrievedStream);
        var retrievedContent = new StreamReader(retrievedStream!).ReadToEnd();

        // Assert
        Assert.Equal(originalContent, retrievedContent);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveBlobSuccessfully()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/delete-blob.txt";
        var content = "Content to be deleted from Azure";
        await _storageProvider!.PutStringAsync(blobName, content);

        // Verify exists before deletion
        var existsBefore = await _storageProvider.ExistsAsync(blobName);
        Assert.True(existsBefore);

        // Act
        var deleteResult = await _storageProvider.DeleteAsync(blobName);

        // Assert
        Assert.True(deleteResult);
        var existsAfter = await _storageProvider.ExistsAsync(blobName);
        Assert.False(existsAfter);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingBlob()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/exists-blob.txt";
        var content = "Blob that exists in Azure";
        await _storageProvider!.PutStringAsync(blobName, content);

        // Act
        var exists = await _storageProvider.ExistsAsync(blobName);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseForNonExistingBlob()
    {
        // Arrange
        var containerName = $"test-container-{Guid.NewGuid().ToString("N")}";
        var blobName = $"{containerName}/non-existing-blob.txt";

        // Act
        var exists = await _storageProvider.ExistsAsync(blobName);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task MultipleContainers_ShouldWorkIndependently()
    {
        // Arrange
        var container1 = $"test-container-1-{Guid.NewGuid().ToString("N")}";
        var container2 = $"test-container-2-{Guid.NewGuid().ToString("N")}";
        var blob1 = $"{container1}/blob1.txt";
        var blob2 = $"{container2}/blob2.txt";
        var content1 = "Content in container 1";
        var content2 = "Content in container 2";

        // Act
        await _storageProvider!.PutStringAsync(blob1, content1);
        await _storageProvider!.PutStringAsync(blob2, content2);

        // Assert
        var retrieved1 = await _storageProvider.GetStringAsync(blob1);
        var retrieved2 = await _storageProvider.GetStringAsync(blob2);

        Assert.Equal(content1, retrieved1);
        Assert.Equal(content2, retrieved2);

        Assert.True(await _storageProvider.ExistsAsync(blob1));
        Assert.True(await _storageProvider.ExistsAsync(blob2));
    }
}