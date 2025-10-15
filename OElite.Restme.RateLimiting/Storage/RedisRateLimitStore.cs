using Microsoft.Extensions.Logging;
using OElite.Abstractions;
using OElite.Providers;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using OElite.Restme.Utils;

namespace OElite.Restme.RateLimiting.Storage;

/// <summary>
/// Redis implementation of rate limit storage using OElite.Restme RedisCacheProvider
/// </summary>
public class RedisRateLimitStore : IRateLimitStore
{
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<RedisRateLimitStore> _logger;

    public RedisRateLimitStore(string connectionString, ILogger<RedisRateLimitStore> logger)
    {
        var config = new RestConfig();
        _cacheProvider = new RedisCacheProvider(connectionString, config);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        try
        {
            var bucket = await _cacheProvider.GetAsync<TokenBucket>(key);
            return bucket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting token bucket for key {Key}", key);
            return null;
        }
    }

    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        try
        {
            await _cacheProvider.SetAsync(bucket.Key, bucket, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting token bucket for key {Key}", bucket.Key);
            throw;
        }
    }

    public async Task<FixedWindow?> GetFixedWindowAsync(string key)
    {
        try
        {
            var window = await _cacheProvider.GetAsync<FixedWindow>(key);
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fixed window for key {Key}", key);
            return null;
        }
    }

    public async Task SetFixedWindowAsync(FixedWindow window)
    {
        try
        {
            await _cacheProvider.SetAsync(window.Key, window, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting fixed window for key {Key}", window.Key);
            throw;
        }
    }

    public async Task<SlidingWindow?> GetSlidingWindowAsync(string key)
    {
        try
        {
            var window = await _cacheProvider.GetAsync<SlidingWindow>(key);
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sliding window for key {Key}", key);
            return null;
        }
    }

    public async Task SetSlidingWindowAsync(string key, SlidingWindow window)
    {
        try
        {
            await _cacheProvider.SetAsync(key, window, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting sliding window for key {Key}", key);
            throw;
        }
    }

    public async Task<LeakyBucket?> GetLeakyBucketAsync(string key)
    {
        try
        {
            var bucket = await _cacheProvider.GetAsync<LeakyBucket>(key);
            return bucket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leaky bucket for key {Key}", key);
            return null;
        }
    }

    public async Task SetLeakyBucketAsync(LeakyBucket bucket)
    {
        try
        {
            await _cacheProvider.SetAsync(bucket.Key, bucket, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting leaky bucket for key {Key}", bucket.Key);
            throw;
        }
    }

    public void Dispose()
    {
        _cacheProvider?.Dispose();
    }
}