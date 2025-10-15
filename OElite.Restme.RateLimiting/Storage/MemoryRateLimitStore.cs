using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;

namespace OElite.Restme.RateLimiting.Storage;

/// <summary>
/// In-memory implementation of rate limit storage
/// </summary>
public class MemoryRateLimitStore : IRateLimitStore
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryRateLimitStore> _logger;

    public MemoryRateLimitStore(IMemoryCache memoryCache, ILogger<MemoryRateLimitStore> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        var bucket = _memoryCache.Get<TokenBucket>(key);
        return await Task.FromResult(bucket);
    }

    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        _memoryCache.Set(bucket.Key, bucket, TimeSpan.FromMinutes(10));
        await Task.CompletedTask;
    }

    public async Task<FixedWindow?> GetFixedWindowAsync(string key)
    {
        var window = _memoryCache.Get<FixedWindow>(key);
        return await Task.FromResult(window);
    }

    public async Task SetFixedWindowAsync(FixedWindow window)
    {
        _memoryCache.Set(window.Key, window, TimeSpan.FromMinutes(10));
        await Task.CompletedTask;
    }

    public async Task<SlidingWindow?> GetSlidingWindowAsync(string key)
    {
        var window = _memoryCache.Get<SlidingWindow>(key);
        return await Task.FromResult(window);
    }

    public async Task SetSlidingWindowAsync(string key, SlidingWindow window)
    {
        _memoryCache.Set(key, window, TimeSpan.FromMinutes(10));
        await Task.CompletedTask;
    }

    public async Task<LeakyBucket?> GetLeakyBucketAsync(string key)
    {
        var bucket = _memoryCache.Get<LeakyBucket>(key);
        return await Task.FromResult(bucket);
    }

    public async Task SetLeakyBucketAsync(LeakyBucket bucket)
    {
        _memoryCache.Set(bucket.Key, bucket, TimeSpan.FromMinutes(10));
        await Task.CompletedTask;
    }
}