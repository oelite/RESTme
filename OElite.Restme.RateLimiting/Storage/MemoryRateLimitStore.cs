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

    public async Task<RateLimitResult> CheckAndIncrementAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = GetWindowStart(now, options.WindowInSeconds);
        var windowEnd = windowStart.AddSeconds(options.WindowInSeconds);
        var cacheKey = $"{options.RedisKeyPrefix}{key}:{windowStart:yyyyMMddHHmm}";

        var currentCount = _memoryCache.Get<long?>(cacheKey) ?? 0;
        var newCount = currentCount + 1;

        // Set cache expiration to window end + buffer
        var expiration = windowEnd.AddMinutes(1);
        _memoryCache.Set(cacheKey, newCount, expiration);

        var isAllowed = newCount <= options.Limit;
        var windowRemainingSeconds = (long)(windowEnd - now).TotalSeconds;

        _logger.LogDebug("Rate limit check for key {Key}: {CurrentCount}/{Limit}, Window: {WindowStart}-{WindowEnd}",
            key, newCount, options.Limit, windowStart, windowEnd);

        return await Task.FromResult(new RateLimitResult
        {
            IsAllowed = isAllowed,
            CurrentCount = newCount,
            Limit = options.Limit,
            WindowRemainingSeconds = Math.Max(0, windowRemainingSeconds),
            WindowResetTime = windowEnd,
            Key = key
        });
    }

    public async Task<RateLimitResult> GetStatusAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = GetWindowStart(now, options.WindowInSeconds);
        var windowEnd = windowStart.AddSeconds(options.WindowInSeconds);
        var cacheKey = $"{options.RedisKeyPrefix}{key}:{windowStart:yyyyMMddHHmm}";

        var currentCount = _memoryCache.Get<long?>(cacheKey) ?? 0;
        var isAllowed = currentCount < options.Limit;
        var windowRemainingSeconds = (long)(windowEnd - now).TotalSeconds;

        return await Task.FromResult(new RateLimitResult
        {
            IsAllowed = isAllowed,
            CurrentCount = currentCount,
            Limit = options.Limit,
            WindowRemainingSeconds = Math.Max(0, windowRemainingSeconds),
            WindowResetTime = windowEnd,
            Key = key
        });
    }

    public async Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default)
    {
        // Memory cache doesn't have a direct way to check if key exists before removal
        // We'll use a pattern-based approach for simplicity
        _memoryCache.Remove(key);
        _logger.LogDebug("Cleared rate limit key: {Key}", key);
        return await Task.FromResult(true);
    }

    public async Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        // MemoryCache doesn't provide a way to clear all keys matching a pattern
        // This would require maintaining a separate index of keys
        // For now, we'll log a warning and return 0
        _logger.LogWarning("ClearAllAsync is not supported for MemoryRateLimitStore. Consider using RedisRateLimitStore for this functionality.");
        return await Task.FromResult(0L);
    }

    private static DateTime GetWindowStart(DateTime now, int windowInSeconds)
    {
        var windowTicks = TimeSpan.FromSeconds(windowInSeconds).Ticks;
        var windowStartTicks = (now.Ticks / windowTicks) * windowTicks;
        return new DateTime(windowStartTicks, DateTimeKind.Utc);
    }
}