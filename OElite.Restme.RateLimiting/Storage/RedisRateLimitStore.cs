using Microsoft.Extensions.Logging;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace OElite.Restme.RateLimiting.Storage;

/// <summary>
/// Redis implementation of rate limit storage for distributed scenarios
/// </summary>
public class RedisRateLimitStore : IRateLimitStore
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisRateLimitStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisRateLimitStore(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisRateLimitStore> logger)
    {
        _database = connectionMultiplexer?.GetDatabase() ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<TokenBucket>((string)value!, _jsonOptions);
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
            var json = JsonSerializer.Serialize(bucket, _jsonOptions);
            await _database.StringSetAsync(bucket.Key, json, TimeSpan.FromMinutes(10));
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
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<FixedWindow>((string)value!, _jsonOptions);
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
            var json = JsonSerializer.Serialize(window, _jsonOptions);
            await _database.StringSetAsync(window.Key, json, TimeSpan.FromMinutes(10));
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
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<SlidingWindow>((string)value!, _jsonOptions);
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
            var json = JsonSerializer.Serialize(window, _jsonOptions);
            await _database.StringSetAsync(key, json, TimeSpan.FromMinutes(10));
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
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<LeakyBucket>((string)value!, _jsonOptions);
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
            var json = JsonSerializer.Serialize(bucket, _jsonOptions);
            await _database.StringSetAsync(bucket.Key, json, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting leaky bucket for key {Key}", bucket.Key);
            throw;
        }
    }
}