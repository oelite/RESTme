using System;
using System.Linq;
using OElite.Restme.Abstractions;
using Xunit;

namespace OElite.Restme.S3.IntegrationTests;

// Test data structures for complex object testing
public class LargeTestObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public NestedTestObject[] NestedObjects { get; set; } = Array.Empty<NestedTestObject>();
}

public class NestedTestObject
{
    public int Index { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class S3CacheIntegrationTests : TestBase
{
    private ICacheProvider? _cacheProvider;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cacheProvider = Rest.GetProvider<ICacheProvider>();
    }


    [Fact]
    public void S3CacheProvider_ShouldImplementIRestmeProvider()
    {
        // Arrange & Act
        var provider = _cacheProvider;

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("S3Cache", provider.ProviderName);
        Assert.Equal(ProviderCapabilities.Cache, provider.Capabilities);
        Assert.NotNull(provider.Configuration);
    }

    [Fact]
    public async Task SetAsync_ShouldStoreValueInS3Cache()
    {
        // Arrange
        var key = $"s3-cache-test-{Guid.NewGuid()}";
        var value = "Test value stored in S3 cache";
        var expiry = TimeSpan.FromMinutes(5);

        // Act
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Assert
        var retrieved = await _cacheProvider.GetAsync<string>(key);
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task GetAsync_ShouldRetrieveValueFromS3Cache()
    {
        // Arrange
        var key = $"s3-retrieve-test-{Guid.NewGuid()}";
        var value = "Value to retrieve from S3 cache";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Act
        var retrieved = await _cacheProvider!.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullForNonExistingS3CacheKey()
    {
        // Arrange
        var key = $"s3-non-existing-{Guid.NewGuid()}";

        // Act
        var retrieved = await _cacheProvider!.GetAsync<string>(key);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveValueFromS3Cache()
    {
        // Arrange
        var key = $"s3-remove-test-{Guid.NewGuid()}";
        var value = "Value to be removed from S3 cache";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Verify exists before removal
        var existsBefore = await _cacheProvider!.GetAsync<string>(key);
        Assert.Equal(value, existsBefore);

        // Act
        var removeResult = await _cacheProvider!.RemoveAsync(key);

        // Assert
        Assert.True(removeResult);
        var existsAfter = await _cacheProvider!.GetAsync<string>(key);
        Assert.Null(existsAfter);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingS3CacheKey()
    {
        // Arrange
        var key = $"s3-exists-test-{Guid.NewGuid()}";
        var value = "S3 cache key that exists";
        var expiry = TimeSpan.FromMinutes(5);
        await _cacheProvider!.SetAsync(key, value, expiry);

        // Act
        var exists = await _cacheProvider!.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseForNonExistingS3CacheKey()
    {
        // Arrange
        var key = $"s3-non-existing-cache-{Guid.NewGuid()}";

        // Act
        var exists = await _cacheProvider!.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task LargeObjectCaching_ShouldWorkWithS3()
    {
        // Arrange
        var key = $"s3-large-test-{Guid.NewGuid()}";
        var largeObject = new LargeTestObject
        {
            Id = Guid.NewGuid(),
            Name = "Large Test Object for S3",
            Data = new byte[1024 * 10], // 10KB of data
            NestedObjects = Enumerable.Range(0, 100).Select(i => new NestedTestObject
            {
                Index = i,
                Value = $"Item {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            }).ToArray()
        };
        var expiry = TimeSpan.FromMinutes(15);

        // Act
        await _cacheProvider!.SetAsync(key, largeObject, expiry);
        var retrieved = await _cacheProvider!.GetAsync<LargeTestObject>(key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(largeObject.Id, retrieved!.Id);
        Assert.Equal(largeObject.Name, retrieved.Name);
        Assert.Equal(largeObject.Data.Length, retrieved.Data.Length);
        Assert.Equal(largeObject.NestedObjects.Length, retrieved.NestedObjects.Length);

        // Verify nested objects
        for (int i = 0; i < Math.Min(5, largeObject.NestedObjects.Length); i++) // Check first 5 items
        {
            Assert.Equal(largeObject.NestedObjects[i].Index, retrieved.NestedObjects[i].Index);
            Assert.Equal(largeObject.NestedObjects[i].Value, retrieved.NestedObjects[i].Value);
            Assert.Equal(largeObject.NestedObjects[i].Timestamp, retrieved.NestedObjects[i].Timestamp);
        }
    }

    [Fact]
    public async Task CacheExpiration_ShouldWorkWithS3Storage()
    {
        // Arrange
        var key = $"s3-expiry-test-{Guid.NewGuid()}";
        var value = "Value that should expire";
        var shortExpiry = TimeSpan.FromSeconds(2); // Very short expiry

        // Act
        await _cacheProvider!.SetAsync(key, value, shortExpiry);

        // Verify exists immediately
        var existsImmediately = await _cacheProvider!.ExistsAsync(key);
        Assert.True(existsImmediately);

        // Wait for expiry
        await Task.Delay(3000); // Wait longer than expiry

        // Assert - Should return false for expired items (application-level expiry validation)
        var stillExists = await _cacheProvider!.ExistsAsync(key);
        Assert.False(stillExists); // Application-level expiry validation should return false for expired items
    }

    [Fact]
    public void S3CacheProvider_ShouldHaveCorrectConfiguration()
    {
        // Arrange & Act
        var config = _cacheProvider!.Configuration;

        // Assert
        Assert.NotNull(config);
        Assert.Contains("endpoint=http://localhost:", config.ConnectionString);
        Assert.Contains("bucket=test-bucket", config.ConnectionString);
        Assert.Equal("AKIAIOSFODNN7EXAMPLE", config.AuthKey);
        Assert.Equal("wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", config.AuthSecret);
    }

    [Fact]
    public void S3CacheProvider_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = _cacheProvider!.Capabilities;

        // Assert
        Assert.Equal(ProviderCapabilities.Cache, capabilities);
    }

    [Fact]
    public void S3CacheProvider_ShouldBeAssignableToICacheProvider()
    {
        // Arrange & Act
        ICacheProvider cacheProvider = _cacheProvider!;

        // Assert
        Assert.NotNull(cacheProvider);
        Assert.IsAssignableFrom<ICacheProvider>(cacheProvider);
    }

    [Fact]
    public void S3CacheProvider_ShouldBeAssignableToIRestmeProvider()
    {
        // Arrange & Act
        IRestmeProvider restmeProvider = _cacheProvider!;

        // Assert
        Assert.NotNull(restmeProvider);
        Assert.IsAssignableFrom<IRestmeProvider>(restmeProvider);
    }
}