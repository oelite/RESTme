using OElite.Providers;
using OElite.Restme.Abstractions;
using Xunit;

namespace OElite.Restme.Azure.IntegrationTests.Tests;

public class AzureCacheIntegrationTests : TestBase
{
    private ICacheProvider? _cacheProvider;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cacheProvider = Restme.GetProvider<ICacheProvider>();
    }


    [Fact]
    public void AzureCacheProvider_ShouldImplementIRestmeProvider()
    {
        // Arrange & Act
        var provider = _cacheProvider;

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("AzureCache", provider.ProviderName);
        Assert.Equal(ProviderCapabilities.Cache, provider.Capabilities);
        Assert.NotNull(provider.Configuration);
    }

    [Fact]
    public async Task SetAsync_ShouldStoreValueInCache()
    {
        // Arrange
        var key = $"azure-cache-test-{Guid.NewGuid()}";
        var value = "Test value stored in Azure cache";
        var expiry = TimeSpan.FromMinutes(5);

        // Act
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Assert
        var retrieved = await _cacheProvider.GetAsync<string>(key);
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task GetAsync_ShouldRetrieveValueFromCache()
    {
        // Arrange
        var key = $"azure-retrieve-test-{Guid.NewGuid()}";
        var value = "Value to retrieve from Azure cache";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Act
        var retrieved = await _cacheProvider.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullForNonExistingKey()
    {
        // Arrange
        var key = $"azure-non-existing-{Guid.NewGuid()}";

        // Act
        var retrieved = await _cacheProvider!.GetAsync<string>(key);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveValueFromCache()
    {
        // Arrange
        var key = $"azure-remove-test-{Guid.NewGuid()}";
        var value = "Value to be removed from Azure cache";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Verify exists before removal
        var existsBefore = await _cacheProvider.GetAsync<string>(key);
        Assert.Equal(value, existsBefore);

        // Act
        var removeResult = await _cacheProvider.RemoveAsync(key);

        // Assert
        Assert.True(removeResult);
        var existsAfter = await _cacheProvider.GetAsync<string>(key);
        Assert.Null(existsAfter);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingCacheKey()
    {
        // Arrange
        var key = $"azure-exists-test-{Guid.NewGuid()}";
        var value = "Cache key that exists";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Act
        var exists = await _cacheProvider.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseForNonExistingCacheKey()
    {
        // Arrange
        var key = $"azure-non-existing-cache-{Guid.NewGuid()}";

        // Act
        var exists = await _cacheProvider.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ComplexObjectCaching_ShouldWorkWithSerialization()
    {
        // Arrange
        var key = $"azure-complex-test-{Guid.NewGuid()}";
        var complexObject = new TestCacheObject
        {
            Id = Guid.NewGuid(),
            Name = "Complex Test Object",
            Values = new[] { 1, 2, 3, 4, 5 },
            Description = "This is a test object for cache serialization",
            Score = 42.5
        };
        var expiry = TimeSpan.FromMinutes(10);

        // Act
        await _cacheProvider!.SetAsync(key, complexObject, expiry);
        var retrieved = await _cacheProvider.GetAsync<TestCacheObject>(key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(complexObject.Id, retrieved.Id);
        Assert.Equal(complexObject.Name, retrieved.Name);
        Assert.Equal(complexObject.Values.Length, retrieved.Values.Length);
        Assert.Equal(complexObject.Description, retrieved.Description);
        Assert.Equal(complexObject.Score, retrieved.Score);
        // Verify array contents
        for (int i = 0; i < complexObject.Values.Length; i++)
        {
            Assert.Equal(complexObject.Values[i], retrieved.Values[i]);
        }
    }

    private class TestCacheObject
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Values { get; set; } = Array.Empty<int>();
        public string Description { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    [Fact]
    public async Task ConcurrentCacheOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();
        var keys = new List<string>();

        // Create multiple concurrent cache operations
        for (int i = 0; i < 5; i++)
        {
            var key = $"concurrent-cache-{Guid.NewGuid()}-{i}";
            keys.Add(key);

            tasks.Add(Task.Run(async () =>
            {
                var value = $"Concurrent value {Guid.NewGuid()}";
                await _cacheProvider!.SetAsync(key, value, TimeSpan.FromMinutes(5));
                var retrieved = await _cacheProvider.GetAsync<string>(key);
                Assert.Equal(value, retrieved);
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert - All cache operations completed successfully
        foreach (var key in keys)
        {
            var exists = await _cacheProvider!.ExistsAsync(key);
            Assert.True(exists);
        }
    }

    [Fact]
    public void AzureCacheProvider_ShouldHaveCorrectConfiguration()
    {
        // Arrange & Act
        var config = _cacheProvider.Configuration;

        // Assert
        Assert.NotNull(config);
        Assert.Contains("devstoreaccount1", config.ConnectionString);
        Assert.Equal(RestMode.Azure, config.OperationMode);
    }

    [Fact]
    public void AzureCacheProvider_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = _cacheProvider.Capabilities;

        // Assert
        Assert.Equal(ProviderCapabilities.Cache, capabilities);
    }

    [Fact]
    public void AzureCacheProvider_ShouldBeAssignableToICacheProvider()
    {
        // Arrange & Act
        ICacheProvider cacheProvider = _cacheProvider;

        // Assert
        Assert.NotNull(cacheProvider);
        Assert.IsAssignableFrom<ICacheProvider>(cacheProvider);
    }

    [Fact]
    public void AzureCacheProvider_ShouldBeAssignableToIRestmeProvider()
    {
        // Arrange & Act
        IRestmeProvider restmeProvider = _cacheProvider;

        // Assert
        Assert.NotNull(restmeProvider);
        Assert.IsAssignableFrom<IRestmeProvider>(restmeProvider);
    }
}