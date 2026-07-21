using Microsoft.Extensions.Logging;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using StackExchange.Redis;

namespace OElite.Restme.RateLimiting.Storage;

/// <summary>
/// High-performance Redis implementation with shared connection pool and atomic operations
/// </summary>
public class OptimizedRedisRateLimitStore : IRateLimitStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<OptimizedRedisRateLimitStore> _logger;
    private readonly bool _ownsConnection;

    // Lua scripts for atomic operations
    private static readonly string TokenBucketScript = @"
        local key = KEYS[1]
        local capacity = tonumber(ARGV[1])
        local refill_rate = tonumber(ARGV[2])
        local window_seconds = tonumber(ARGV[3])
        local now = tonumber(ARGV[4])
        local expire_seconds = tonumber(ARGV[5])

        local bucket = redis.call('HMGET', key, 'tokens', 'last_refill')
        local tokens = tonumber(bucket[1]) or capacity
        local last_refill = tonumber(bucket[2]) or now

        -- Calculate tokens to add
        local time_passed = now - last_refill
        local tokens_to_add = time_passed * refill_rate / window_seconds
        tokens = math.min(capacity, tokens + tokens_to_add)

        -- Check if request can be allowed
        if tokens >= 1 then
            tokens = tokens - 1
            redis.call('HMSET', key, 'tokens', tokens, 'last_refill', now)
            redis.call('EXPIRE', key, expire_seconds)
            return {1, tokens, capacity - tokens}
        else
            redis.call('HMSET', key, 'tokens', tokens, 'last_refill', now)
            redis.call('EXPIRE', key, expire_seconds)
            return {0, tokens, capacity - tokens}
        end
    ";

    private static readonly string FixedWindowScript = @"
        local key = KEYS[1]
        local limit = tonumber(ARGV[1])
        local window_seconds = tonumber(ARGV[2])
        local now = tonumber(ARGV[3])

        local window_start = now - (now % window_seconds)
        local window_key = key .. ':' .. window_start

        local count = redis.call('GET', window_key)
        count = tonumber(count) or 0

        if count < limit then
            count = redis.call('INCR', window_key)
            redis.call('EXPIRE', window_key, window_seconds * 2)
            return {1, count, limit - count}
        else
            return {0, count, 0}
        end
    ";

    public OptimizedRedisRateLimitStore(IConnectionMultiplexer redis, ILogger<OptimizedRedisRateLimitStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _database = _redis.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsConnection = false;
    }

    public OptimizedRedisRateLimitStore(string connectionString, ILogger<OptimizedRedisRateLimitStore> logger)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.ConnectTimeout = 10000;
        config.SyncTimeout = 5000;
        config.AsyncTimeout = 5000;
        config.AbortOnConnectFail = false;
        config.ConnectRetry = 3;

        _redis = ConnectionMultiplexer.Connect(config);
        _database = _redis.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsConnection = true;
    }

    /// <summary>
    /// Atomic token bucket check and update operation using Lua script
    /// </summary>
    public async Task<(bool allowed, double tokens, double used)> CheckAndUpdateTokenBucketAsync(
        string key, int capacity, double refillRate, int windowSeconds)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var result = await _database.ScriptEvaluateAsync(TokenBucketScript, new RedisKey[] { key },
                new RedisValue[] { capacity, refillRate, windowSeconds, now, 600 });

            var values = (RedisValue[])result!;
            return (values[0] == 1, (double)values[1], (double)values[2]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in atomic token bucket check for key {Key}", key);
            return (true, capacity, 0); // Fail open
        }
    }

    /// <summary>
    /// Atomic fixed window check and update operation using Lua script
    /// </summary>
    public async Task<(bool allowed, long count, long remaining)> CheckAndUpdateFixedWindowAsync(
        string key, int limit, int windowSeconds)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var result = await _database.ScriptEvaluateAsync(FixedWindowScript, new RedisKey[] { key },
                new RedisValue[] { limit, windowSeconds, now });

            var values = (RedisValue[])result!;
            return (values[0] == 1, (long)values[1], (long)values[2]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in atomic fixed window check for key {Key}", key);
            return (true, 0, limit); // Fail open
        }
    }

    // Standard interface implementations for backward compatibility
    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        try
        {
            var values = await _database.HashGetAllAsync(key);
            if (values.Length == 0) return null;

            var bucket = new TokenBucket
            {
                Key = key,
                Capacity = (int)values.FirstOrDefault(v => v.Name == "capacity").Value,
                Tokens = (double)values.FirstOrDefault(v => v.Name == "tokens").Value,
                LastRefillTime = DateTime.FromBinary((long)values.FirstOrDefault(v => v.Name == "last_refill").Value),
                RefillRate = (double)values.FirstOrDefault(v => v.Name == "refill_rate").Value
            };
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
            await _database.HashSetAsync(bucket.Key, new HashEntry[]
            {
                new("capacity", bucket.Capacity),
                new("tokens", bucket.Tokens),
                new("last_refill", bucket.LastRefillTime.ToBinary()),
                new("refill_rate", bucket.RefillRate)
            });
            await _database.KeyExpireAsync(bucket.Key, TimeSpan.FromMinutes(10));
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
            var values = await _database.HashGetAllAsync(key);
            if (values.Length == 0) return null;

            var window = new FixedWindow
            {
                Key = key,
                StartTime = DateTime.FromBinary((long)values.FirstOrDefault(v => v.Name == "start_time").Value),
                Count = (int)values.FirstOrDefault(v => v.Name == "count").Value
            };
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
            await _database.HashSetAsync(window.Key, new HashEntry[]
            {
                new("start_time", window.StartTime.ToBinary()),
                new("count", window.Count)
            });
            await _database.KeyExpireAsync(window.Key, TimeSpan.FromMinutes(10));
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
            var count = await _database.SortedSetLengthAsync(key);
            if (count == 0) return null;

            var window = new SlidingWindow(60); // Default window size
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
            await _database.KeyExpireAsync(key, TimeSpan.FromMinutes(10));
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
            var values = await _database.HashGetAllAsync(key);
            if (values.Length == 0) return null;

            var bucket = new LeakyBucket
            {
                Key = key,
                Capacity = (int)values.FirstOrDefault(v => v.Name == "capacity").Value,
                Level = (double)values.FirstOrDefault(v => v.Name == "level").Value,
                LastLeakTime = DateTime.FromBinary((long)values.FirstOrDefault(v => v.Name == "last_leak").Value),
                LeakRate = (double)values.FirstOrDefault(v => v.Name == "leak_rate").Value
            };
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
            await _database.HashSetAsync(bucket.Key, new HashEntry[]
            {
                new("capacity", bucket.Capacity),
                new("level", bucket.Level),
                new("last_leak", bucket.LastLeakTime.ToBinary()),
                new("leak_rate", bucket.LeakRate)
            });
            await _database.KeyExpireAsync(bucket.Key, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting leaky bucket for key {Key}", bucket.Key);
            throw;
        }
    }

    public async Task<int> ScanAndDeleteKeysAsync(string pattern)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (keys.Length == 0)
            {
                _logger.LogDebug("No keys found matching pattern: {Pattern}", pattern);
                return 0;
            }

            var deletedCount = 0;
            const int batchSize = 100;

            for (int i = 0; i < keys.Length; i += batchSize)
            {
                var batch = keys.Skip(i).Take(batchSize).ToArray();
                deletedCount += (int)await _database.KeyDeleteAsync(batch);
            }

            _logger.LogInformation("Deleted {Count} keys matching pattern: {Pattern}", deletedCount, pattern);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning and deleting Redis keys for pattern: {Pattern}", pattern);
            return 0;
        }
    }

    public void Dispose()
    {
        if (_ownsConnection)
        {
            _redis?.Dispose();
        }
    }
}