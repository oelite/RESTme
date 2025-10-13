using Microsoft.Extensions.Caching.Distributed;
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

    public async Task<RateLimitResult> CheckAndIncrementAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = GetWindowStart(now, options.WindowInSeconds);
        var windowEnd = windowStart.AddSeconds(options.WindowInSeconds);
        var redisKey = $"{options.RedisKeyPrefix}{key}:{windowStart:yyyyMMddHHmmss}";

        try
        {
            // Use Redis INCR for atomic increment
            var currentCount = await _database.StringIncrementAsync(redisKey);

            // Set expiration on first increment
            if (currentCount == 1)
            {
                var expiration = windowEnd.AddMinutes(1) - now;
                await _database.KeyExpireAsync(redisKey, expiration);
            }

            var isAllowed = currentCount <= options.Limit;
            var windowRemainingSeconds = (long)(windowEnd - now).TotalSeconds;

            _logger.LogDebug("Redis rate limit check for key {Key}: {CurrentCount}/{Limit}, Window: {WindowStart}-{WindowEnd}",
                key, currentCount, options.Limit, windowStart, windowEnd);

            return new RateLimitResult
            {
                IsAllowed = isAllowed,
                CurrentCount = currentCount,
                Limit = options.Limit,
                WindowRemainingSeconds = Math.Max(0, windowRemainingSeconds),
                WindowResetTime = windowEnd,
                Key = key
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing rate limit for key {Key}", key);
            throw;
        }
    }

    public async Task<RateLimitResult> GetStatusAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = GetWindowStart(now, options.WindowInSeconds);
        var windowEnd = windowStart.AddSeconds(options.WindowInSeconds);
        var redisKey = $"{options.RedisKeyPrefix}{key}:{windowStart:yyyyMMddHHmmss}";

        try
        {
            var currentCountValue = await _database.StringGetAsync(redisKey);
            var currentCount = currentCountValue.HasValue ? (long)currentCountValue : 0L;
            var isAllowed = currentCount < options.Limit;
            var windowRemainingSeconds = (long)(windowEnd - now).TotalSeconds;

            return new RateLimitResult
            {
                IsAllowed = isAllowed,
                CurrentCount = currentCount,
                Limit = options.Limit,
                WindowRemainingSeconds = Math.Max(0, windowRemainingSeconds),
                WindowResetTime = windowEnd,
                Key = key
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit status for key {Key}", key);
            throw;
        }
    }

    public async Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Cleared rate limit key: {Key}, Existed: {Existed}", key, deleted);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing rate limit key {Key}", key);
            throw;
        }
    }

    public async Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: "rate_limit:*").ToArray();

            if (keys.Length == 0)
                return 0;

            var deleted = await _database.KeyDeleteAsync(keys);
            _logger.LogInformation("Cleared {Count} rate limit keys", deleted);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all rate limit keys");
            throw;
        }
    }

    private static DateTime GetWindowStart(DateTime now, int windowInSeconds)
    {
        var windowTicks = TimeSpan.FromSeconds(windowInSeconds).Ticks;
        var windowStartTicks = (now.Ticks / windowTicks) * windowTicks;
        return new DateTime(windowStartTicks, DateTimeKind.Utc);
    }
}