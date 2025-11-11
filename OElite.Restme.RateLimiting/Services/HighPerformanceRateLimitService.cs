using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using OElite.Restme.RateLimiting.Storage;
using System.Collections.Concurrent;
using System.Net;

namespace OElite.Restme.RateLimiting.Services;

/// <summary>
/// High-performance rate limiting service with optimized Redis operations and parallel execution
/// </summary>
public class HighPerformanceRateLimitService : IRateLimitService
{
    private readonly IRateLimitStore _store;
    private readonly OptimizedRedisRateLimitStore? _optimizedStore;
    private readonly ILogger<HighPerformanceRateLimitService> _logger;
    private readonly bool _useOptimizedOperations;

    // DDoS detection using sliding windows
    private readonly ConcurrentDictionary<string, SlidingWindow> _ddosWindows;
    private readonly ConcurrentDictionary<string, DateTime> _blockedIps;

    // Server load monitoring for adaptive limiting
    private readonly Timer _loadMonitoringTimer;
    private readonly Timer _cleanupTimer;
    private double _currentServerLoad = 0.0;

    public HighPerformanceRateLimitService(
        IRateLimitStore store,
        ILogger<HighPerformanceRateLimitService> logger)
    {
        _store = store;
        _optimizedStore = store as OptimizedRedisRateLimitStore;
        _useOptimizedOperations = _optimizedStore != null;
        _logger = logger;
        _ddosWindows = new ConcurrentDictionary<string, SlidingWindow>();
        _blockedIps = new ConcurrentDictionary<string, DateTime>();

        // Monitor server load every 30 seconds for adaptive limiting
        _loadMonitoringTimer = new Timer(MonitorServerLoad, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        // Cleanup expired entries every 5 minutes to prevent memory leaks
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        if (_useOptimizedOperations)
        {
            _logger.LogInformation("High-performance atomic Redis operations enabled");
        }
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options)
    {
        var clientIp = GetClientIpAddress(context);
        var key = GenerateKey(clientIp, options);

        try
        {
            // Execute checks with optimized parallel execution
            var tasks = new List<Task>();

            // 1. IP block check (fast, memory-based)
            var isBlocked = await IsIpBlockedAsync(clientIp);
            if (isBlocked)
            {
                return CreateBlockedResult(key, "IP is currently blocked", options);
            }

            // 2. DDoS Protection Check (if enabled)
            Task<RateLimitResult>? ddosTask = null;
            if (options.EnableDDoSProtection)
            {
                ddosTask = CheckDDoSProtectionAsync(clientIp, options);
                tasks.Add(ddosTask);
            }

            // 3. Apply adaptive rate limiting if enabled
            var effectiveLimit = options.Limit;
            var effectiveBurstCapacity = options.BurstCapacity;

            if (options.EnableAdaptiveLimiting)
            {
                var adaptiveLimits = CalculateAdaptiveLimits(options);
                effectiveLimit = adaptiveLimits.Limit;
                effectiveBurstCapacity = adaptiveLimits.BurstCapacity;
            }

            // 4. Main rate limit check using optimized operations
            var rateLimitTask = CheckRateLimitWithOptimizedOperations(key, effectiveLimit, effectiveBurstCapacity, options);
            tasks.Add(rateLimitTask);

            // 5. Domain-based rate limiting (if enabled)
            Task<RateLimitResult>? domainTask = null;
            if (options.EnableDomainRateLimiting)
            {
                var domainKey = GenerateDomainKey(context.Request.Host.Host, options);
                domainTask = CheckRateLimitWithOptimizedOperations(domainKey, options.DomainRequestsPerMinute, options.DomainBurstCapacity, options);
                tasks.Add(domainTask);
            }

            // Await all parallel checks
            await Task.WhenAll(tasks);

            // Check results in priority order
            if (ddosTask != null)
            {
                var ddosResult = await ddosTask;
                if (ddosResult.IsBlocked)
                {
                    return ddosResult;
                }
            }

            var result = await rateLimitTask;
            if (!result.IsAllowed)
            {
                return result;
            }

            if (domainTask != null)
            {
                var domainResult = await domainTask;
                if (!domainResult.IsAllowed)
                {
                    return CreateBlockedResult(key, "Domain rate limit exceeded", options);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key {Key}", key);
            // Fail open - allow request if rate limiting fails
            return new RateLimitResult
            {
                IsAllowed = true,
                Key = key,
                CurrentCount = 0,
                Limit = options.Limit,
                WindowRemainingSeconds = options.WindowInSeconds,
                WindowResetTime = DateTime.UtcNow.AddSeconds(options.WindowInSeconds)
            };
        }
    }

    private async Task<RateLimitResult> CheckRateLimitWithOptimizedOperations(string key, int limit, int burstCapacity, RateLimitOptions options)
    {
        var now = DateTime.UtcNow;

        // Use optimized atomic operations if available
        if (_useOptimizedOperations && _optimizedStore != null)
        {
            try
            {
                var refillRate = (double)limit / options.WindowInSeconds;
                var (allowed, tokens, used) = await _optimizedStore.CheckAndUpdateTokenBucketAsync(
                    key, burstCapacity, refillRate, options.WindowInSeconds);

                return new RateLimitResult
                {
                    IsAllowed = allowed,
                    Key = key,
                    CurrentCount = (long)used,
                    Limit = limit,
                    RemainingRequests = (long)tokens,
                    WindowRemainingSeconds = allowed ? (int)((burstCapacity - tokens) / refillRate) : (int)(1 / refillRate),
                    WindowResetTime = allowed ? now.AddSeconds((burstCapacity - tokens) / refillRate) : now.AddSeconds(1 / refillRate)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Optimized operation failed for key {Key}, falling back to standard operations", key);
                // Fall through to standard implementation
            }
        }

        // Standard fallback implementation
        return await CheckTokenBucketStandard(key, limit, burstCapacity, options);
    }

    private async Task<RateLimitResult> CheckTokenBucketStandard(string key, int limit, int burstCapacity, RateLimitOptions options)
    {
        var bucket = await _store.GetTokenBucketAsync(key);
        var now = DateTime.UtcNow;
        var refillRate = (double)limit / options.WindowInSeconds;

        if (bucket == null)
        {
            bucket = new TokenBucket
            {
                Key = key,
                Capacity = burstCapacity,
                Tokens = burstCapacity,
                LastRefillTime = now,
                RefillRate = refillRate
            };
        }

        var timePassed = (now - bucket.LastRefillTime).TotalSeconds;
        var tokensToAdd = timePassed * bucket.RefillRate;

        bucket.Tokens = Math.Min(bucket.Capacity, bucket.Tokens + tokensToAdd);
        bucket.LastRefillTime = now;

        if (bucket.Tokens >= 1)
        {
            bucket.Tokens -= 1;
            await _store.SetTokenBucketAsync(bucket);

            return new RateLimitResult
            {
                IsAllowed = true,
                Key = key,
                CurrentCount = (long)(burstCapacity - bucket.Tokens),
                Limit = limit,
                RemainingRequests = (long)bucket.Tokens,
                WindowRemainingSeconds = (int)((bucket.Capacity - bucket.Tokens) / bucket.RefillRate),
                WindowResetTime = now.AddSeconds((bucket.Capacity - bucket.Tokens) / bucket.RefillRate)
            };
        }

        return new RateLimitResult
        {
            IsAllowed = false,
            Key = key,
            CurrentCount = burstCapacity,
            Limit = limit,
            RemainingRequests = 0,
            WindowRemainingSeconds = (int)(1 / bucket.RefillRate),
            WindowResetTime = now.AddSeconds(1 / bucket.RefillRate)
        };
    }

    private async Task<RateLimitResult> CheckDDoSProtectionAsync(string clientIp, RateLimitOptions options)
    {
        var ddosKey = $"ddos:{clientIp}";
        var window = _ddosWindows.GetOrAdd(ddosKey, _ => new SlidingWindow(options.WindowInSeconds));

        var now = DateTime.UtcNow;
        window.AddRequest(now);
        var requestCount = window.GetRequestCount(now);

        if (requestCount > options.DDoSThreshold)
        {
            var blockMinutes = options.DDoSBlockMinutes;
            if (options.ProgressiveBlocking)
            {
                var severity = (double)requestCount / options.DDoSThreshold;
                if (severity >= 3.0) blockMinutes *= 4;
                else if (severity >= 2.0) blockMinutes *= 2;
            }

            var blockUntil = now.AddMinutes(blockMinutes);
            await BlockIpAsync(clientIp, blockUntil);

            _logger.LogWarning("DDoS pattern detected for {ClientIp}. Requests: {RequestCount}, Block duration: {BlockMinutes} minutes",
                clientIp, requestCount, blockMinutes);

            return new RateLimitResult
            {
                IsAllowed = false,
                IsBlocked = true,
                Key = ddosKey,
                RemainingRequests = 0,
                ResetTimeUtc = blockUntil,
                RetryAfterSeconds = (int)(blockUntil - now).TotalSeconds,
                BlockReason = $"DDoS protection activated ({requestCount} requests)"
            };
        }

        return new RateLimitResult { IsAllowed = true };
    }

    private (int Limit, int BurstCapacity) CalculateAdaptiveLimits(RateLimitOptions options)
    {
        if (_currentServerLoad <= options.ServerLoadThreshold)
        {
            return (options.Limit, options.BurstCapacity);
        }

        var loadFactor = 1.0 - (_currentServerLoad - options.ServerLoadThreshold);
        var adaptiveLimit = (int)(options.Limit * loadFactor);
        var adaptiveBurst = (int)(options.BurstCapacity * loadFactor);

        return (Math.Max(1, adaptiveLimit), Math.Max(1, adaptiveBurst));
    }

    private Task<bool> IsIpBlockedAsync(string clientIp)
    {
        if (_blockedIps.TryGetValue(clientIp, out var blockUntil))
        {
            if (DateTime.UtcNow < blockUntil)
            {
                return Task.FromResult(true);
            }
            else
            {
                _blockedIps.TryRemove(clientIp, out _);
            }
        }
        return Task.FromResult(false);
    }

    private Task BlockIpAsync(string clientIp, DateTime blockUntil)
    {
        _blockedIps[clientIp] = blockUntil;
        return Task.CompletedTask;
    }

    private RateLimitResult CreateBlockedResult(string key, string reason, RateLimitOptions options)
    {
        return new RateLimitResult
        {
            IsAllowed = false,
            IsBlocked = true,
            Key = key,
            BlockReason = reason,
            RetryAfterSeconds = options.WindowInSeconds,
            ResetTimeUtc = DateTime.UtcNow.AddSeconds(options.WindowInSeconds)
        };
    }

    private string GenerateKey(string clientIp, RateLimitOptions options)
    {
        var prefix = options.KeyPrefix ?? "rate_limit";
        return $"{prefix}:{clientIp}";
    }

    private string GenerateDomainKey(string domain, RateLimitOptions options)
    {
        var prefix = options.KeyPrefix ?? "domain_rate_limit";
        return $"{prefix}:{domain}";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0 && IPAddress.TryParse(ips[0].Trim(), out var ip))
            {
                return ip.ToString();
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out var realIpAddress))
        {
            return realIpAddress.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void MonitorServerLoad(object? state)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            _currentServerLoad = process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
            _currentServerLoad = Math.Min(1.0, _currentServerLoad);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error monitoring server load");
            _currentServerLoad = 0.0;
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var cutoffTime = now.AddHours(-1);

            var expiredBlockedIps = _blockedIps
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ip in expiredBlockedIps)
            {
                _blockedIps.TryRemove(ip, out _);
            }

            var expiredDdosWindows = _ddosWindows
                .Where(kvp => kvp.Value.GetRequestCount(now) == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredDdosWindows)
            {
                _ddosWindows.TryRemove(key, out _);
            }

            if (expiredBlockedIps.Count > 0 || expiredDdosWindows.Count > 0)
            {
                _logger.LogDebug("Cleaned up {BlockedIps} blocked IPs and {DdosWindows} DDoS windows",
                    expiredBlockedIps.Count, expiredDdosWindows.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during cleanup of expired entries");
        }
    }

    public void Dispose()
    {
        _loadMonitoringTimer?.Dispose();
        _cleanupTimer?.Dispose();
    }
}