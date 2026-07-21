using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OElite.Restme.Base;
using OElite.Restme.RateLimiting.Models;
using OElite.Restme.RateLimiting.Storage;
using Xunit;

namespace OElite.Restme.RateLimiting.UnitTests;

/// <summary>
/// Unit tests for MemoryRateLimitStore, specifically the ScanAndDeleteKeysAsync method
/// and the underlying MemoryCacheProvider.GetKeys pattern matching.
/// These tests require no Docker or external infrastructure.
/// </summary>
public class MemoryRateLimitStoreTests
{
    private static MemoryRateLimitStore CreateStore()
    {
        var logger = NullLogger<MemoryRateLimitStore>.Instance;
        return new MemoryRateLimitStore(logger);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldReturnZero_WhenPatternIsEmpty()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = await store.ScanAndDeleteKeysAsync(string.Empty);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldReturnZero_WhenPatternIsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = await store.ScanAndDeleteKeysAsync(null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldReturnZero_WhenNoKeysMatch()
    {
        // Arrange
        var store = CreateStore();
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "rate_limit:user:alice",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });

        // Act
        var result = await store.ScanAndDeleteKeysAsync("rate_limit:user:bob");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldDeleteMatchingKeys_WithWildcardPattern()
    {
        // Arrange
        var store = CreateStore();
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "rate_limit:user:alice",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });
        await store.SetFixedWindowAsync(new FixedWindow
        {
            Key = "rate_limit:user:bob",
            StartTime = DateTime.UtcNow,
            Count = 5
        });
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "cache:session:xyz",
            Capacity = 10,
            Tokens = 5,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 1
        });

        // Act
        var deleted = await store.ScanAndDeleteKeysAsync("rate_limit:*");

        // Assert
        deleted.Should().Be(2);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldHandleSingleCharacterWildcard()
    {
        // Arrange
        var store = CreateStore();
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "user:abc:limit",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "user:abd:limit",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "user:abcd:limit",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });

        // Act
        var deleted = await store.ScanAndDeleteKeysAsync("user:abc?:limit");

        // Assert
        // "?" matches exactly 1 char, so "user:abc:limit" (0 extra chars) and "user:abcd:limit" (1 extra char)
        // "user:abc:limit" -> "abc" then "?" must match ":" -> yes, then ":limit" — wait, pattern is "user:abc?:limit"
        // Pattern breakdown: "user:" + "abc" + "?" + ":limit"
        // "user:abc:limit"  -> "abc" + ":" + ":limit" — no, the ":" after abc is the separator, then "limit" not ":limit"
        // Actually: "user:abc?:limit" means "user:abc" + 1 char + ":limit"
        // "user:abc:limit"  -> after "user:abc" we have ":limit" — that's 0 extra chars before ":limit"? No: ":limit" starts with ":"
        //   "user:abc" + "?" + ":limit" — "?" must match exactly 1 char
        //   "user:abc:limit" -> after "user:abc" remaining is ":limit" — "?" matches ":", then ":limit" must match "limit" — NO
        // "user:abcd:limit" -> after "user:abc" remaining is "d:limit" — "?" matches "d", then ":limit" matches ":limit" — YES
        deleted.Should().Be(1);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldHandleExactMatchPattern()
    {
        // Arrange
        var store = CreateStore();
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "exact_key",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "exact_key_other",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });

        // Act
        var deleted = await store.ScanAndDeleteKeysAsync("exact_key");

        // Assert
        deleted.Should().Be(1);
    }

    [Fact]
    public async Task ScanAndDeleteKeysAsync_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        // Arrange
        var store = CreateStore();
        await store.SetTokenBucketAsync(new TokenBucket
        {
            Key = "rate_limit:user:alice",
            Capacity = 100,
            Tokens = 50,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 10
        });

        // Act
        var firstPass = await store.ScanAndDeleteKeysAsync("rate_limit:*");
        var secondPass = await store.ScanAndDeleteKeysAsync("rate_limit:*");

        // Assert
        firstPass.Should().Be(1);
        secondPass.Should().Be(0);
    }
}

/// <summary>
/// Unit tests for MemoryCacheProvider.GetKeys pattern matching
/// </summary>
public class MemoryCacheProviderGetKeysTests
{
    [Fact]
    public void GetKeys_ShouldReturnEmpty_WhenPatternIsEmpty()
    {
        // Arrange
        var provider = new MemoryCacheProvider();

        // Act
        var keys = provider.GetKeys(string.Empty);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void GetKeys_ShouldReturnEmpty_WhenPatternIsNull()
    {
        // Arrange
        var provider = new MemoryCacheProvider();

        // Act
        var keys = provider.GetKeys(null!);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void GetKeys_ShouldReturnEmpty_WhenCacheIsEmpty()
    {
        // Arrange
        var provider = new MemoryCacheProvider();

        // Act
        var keys = provider.GetKeys("*");

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKeys_ShouldMatchAll_WithAsteriskWildcard()
    {
        // Arrange
        var provider = new MemoryCacheProvider();
        await provider.SetAsync("key1", "value1", TimeSpan.FromMinutes(10));
        await provider.SetAsync("key2", "value2", TimeSpan.FromMinutes(10));
        await provider.SetAsync("other", "value3", TimeSpan.FromMinutes(10));

        // Act
        var keys = provider.GetKeys("key*");

        // Assert
        keys.Should().HaveCount(2);
        keys.Should().Contain("key1");
        keys.Should().Contain("key2");
    }

    [Fact]
    public async Task GetKeys_ShouldFilterExpiredKeys()
    {
        // Arrange
        var provider = new MemoryCacheProvider();
        await provider.SetAsync("fresh_key", "fresh", TimeSpan.FromMinutes(10));
        await provider.SetAsync("stale_key", "stale", TimeSpan.FromMilliseconds(1));

        // Wait for the stale key to expire
        Thread.Sleep(10);

        // Act
        var keys = provider.GetKeys("*_key");

        // Assert
        keys.Should().HaveCount(1);
        keys.Should().Contain("fresh_key");
    }

    [Fact]
    public async Task GetKeys_ShouldMatchDotInKey_WhenPatternHasDot()
    {
        // Arrange
        var provider = new MemoryCacheProvider();
        await provider.SetAsync("namespace.key", "value1", TimeSpan.FromMinutes(10));
        await provider.SetAsync("namespace_key", "value2", TimeSpan.FromMinutes(10));

        // Act
        var keys = provider.GetKeys("namespace.*");

        // Assert
        keys.Should().HaveCount(1);
        keys.Should().Contain("namespace.key");
    }

    [Fact]
    public async Task GetKeys_ShouldMatchSingleCharacter_WithQuestionMark()
    {
        // Arrange
        var provider = new MemoryCacheProvider();
        await provider.SetAsync("cat", "value1", TimeSpan.FromMinutes(10));
        await provider.SetAsync("car", "value2", TimeSpan.FromMinutes(10));
        await provider.SetAsync("cart", "value3", TimeSpan.FromMinutes(10));

        // Act
        var keys = provider.GetKeys("ca?");

        // Assert
        keys.Should().HaveCount(2);
        keys.Should().Contain("cat");
        keys.Should().Contain("car");
    }

    [Fact]
    public async Task GetKeys_ShouldHandleMixedWildcards()
    {
        // Arrange
        var provider = new MemoryCacheProvider();
        await provider.SetAsync("a:b:c", "v1", TimeSpan.FromMinutes(10));
        await provider.SetAsync("a:b:d", "v2", TimeSpan.FromMinutes(10));
        await provider.SetAsync("a:c:e", "v3", TimeSpan.FromMinutes(10));

        // Act
        var keys = provider.GetKeys("a:b:?");

        // Assert
        keys.Should().HaveCount(2);
        keys.Should().Contain("a:b:c");
        keys.Should().Contain("a:b:d");
    }
}

/// <summary>
/// Unit tests for RateLimitOptions fallback properties
/// </summary>
public class RateLimitOptionsFallbackTests
{
    [Fact]
    public void FallbackIpAddress_ShouldDefaultToLocalhost()
    {
        // Arrange & Act
        var options = new RateLimitOptions();

        // Assert
        options.FallbackIpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public void FallbackIpAddress_ShouldBeSettable()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Act
        options.FallbackIpAddress = "192.168.1.1";

        // Assert
        options.FallbackIpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public void FallbackMaxRequests_ShouldDefaultToOneThousand()
    {
        // Arrange & Act
        var options = new RateLimitOptions();

        // Assert
        options.FallbackMaxRequests.Should().Be(1000);
    }

    [Fact]
    public void FallbackMaxRequests_ShouldBeSettable()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Act
        options.FallbackMaxRequests = 500;

        // Assert
        options.FallbackMaxRequests.Should().Be(500);
    }

    [Fact]
    public void FallbackWindowSeconds_ShouldDefaultToSixty()
    {
        // Arrange & Act
        var options = new RateLimitOptions();

        // Assert
        options.FallbackWindowSeconds.Should().Be(60);
    }

    [Fact]
    public void FallbackWindowSeconds_ShouldBeSettable()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Act
        options.FallbackWindowSeconds = 120;

        // Assert
        options.FallbackWindowSeconds.Should().Be(120);
    }

    [Fact]
    public void AllFallbackProperties_ShouldBeIndependent()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Act
        options.FallbackIpAddress = "10.0.0.1";
        options.FallbackMaxRequests = 2000;
        options.FallbackWindowSeconds = 300;

        // Assert
        options.FallbackIpAddress.Should().Be("10.0.0.1");
        options.FallbackMaxRequests.Should().Be(2000);
        options.FallbackWindowSeconds.Should().Be(300);
    }
}
