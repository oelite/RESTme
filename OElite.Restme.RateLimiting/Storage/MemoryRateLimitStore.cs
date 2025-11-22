using Microsoft.Extensions.Logging;
using OElite.Restme.Abstractions;
using OElite.Restme.Base;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;

namespace OElite.Restme.RateLimiting.Storage;

/// <summary>
/// In-memory implementation of rate limit storage using OElite.Restme MemoryCacheProvider
/// </summary>
public class MemoryRateLimitStore : IRateLimitStore
{
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<MemoryRateLimitStore> _logger;

    public MemoryRateLimitStore(ILogger<MemoryRateLimitStore> logger)
    {
        _cacheProvider = new MemoryCacheProvider();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        var bucket = await _cacheProvider.GetAsync<TokenBucket>(key);
        return bucket;
    }

    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        await _cacheProvider.SetAsync(bucket.Key, bucket, TimeSpan.FromMinutes(10));
    }

    public async Task<FixedWindow?> GetFixedWindowAsync(string key)
    {
        var window = await _cacheProvider.GetAsync<FixedWindow>(key);
        return window;
    }

    public async Task SetFixedWindowAsync(FixedWindow window)
    {
        await _cacheProvider.SetAsync(window.Key, window, TimeSpan.FromMinutes(10));
    }

    public async Task<SlidingWindow?> GetSlidingWindowAsync(string key)
    {
        var window = await _cacheProvider.GetAsync<SlidingWindow>(key);
        return window;
    }

    public async Task SetSlidingWindowAsync(string key, SlidingWindow window)
    {
        await _cacheProvider.SetAsync(key, window, TimeSpan.FromMinutes(10));
    }

    public async Task<LeakyBucket?> GetLeakyBucketAsync(string key)
    {
        var bucket = await _cacheProvider.GetAsync<LeakyBucket>(key);
        return bucket;
    }

    public async Task SetLeakyBucketAsync(LeakyBucket bucket)
    {
        await _cacheProvider.SetAsync(bucket.Key, bucket, TimeSpan.FromMinutes(10));
    }

    public void Dispose()
    {
        _cacheProvider?.Dispose();
    }
}