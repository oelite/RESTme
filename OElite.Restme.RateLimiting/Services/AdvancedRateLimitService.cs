using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OElite.Abstractions;
using OElite.Base;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using OElite.Restme.RateLimiting.Storage;
using System.Collections.Concurrent;
using System.Net;
using System.Diagnostics;

namespace OElite.Restme.RateLimiting.Services;

/// <summary>
/// Advanced rate limiting service with DDoS protection, adaptive limiting, and emergency mode
/// Implements enterprise-grade rate limiting features from Kortex
/// </summary>
public class AdvancedRateLimitService : IRateLimitService
{
    private readonly IRateLimitStore _store;
    private readonly ILogger<AdvancedRateLimitService> _logger;
    private readonly ICacheProvider _cacheProvider;

    // DDoS detection using sliding windows
    private readonly ConcurrentDictionary<string, SlidingWindow> _ddosWindows;
    private readonly ConcurrentDictionary<string, DateTime> _blockedIps;

    // Server load monitoring for adaptive limiting
    private readonly Timer _loadMonitoringTimer;
    private readonly Timer _cleanupTimer;
    private double _currentServerLoad = 0.0;

    public AdvancedRateLimitService(
        IRateLimitStore store,
        ILogger<AdvancedRateLimitService> logger)
    {
        _store = store;
        _logger = logger;
        _cacheProvider = new MemoryCacheProvider();
        _ddosWindows = new ConcurrentDictionary<string, SlidingWindow>();
        _blockedIps = new ConcurrentDictionary<string, DateTime>();

        // Monitor server load every 30 seconds for adaptive limiting
        _loadMonitoringTimer = new Timer(MonitorServerLoad, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        
        // Cleanup expired entries every 5 minutes to prevent memory leaks
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options)
    {
        var clientIp = GetClientIpAddress(context);
        var key = GenerateKey(clientIp, options);

        try
        {
            // 1. Check if IP is currently blocked
            if (await IsIpBlockedAsync(clientIp))
            {
                return CreateBlockedResult(key, "IP is currently blocked", options);
            }

            // 2. DDoS Protection Check
            if (options.EnableDDoSProtection)
            {
                var ddosResult = await CheckDDoSProtectionAsync(clientIp, options);
                if (ddosResult.IsBlocked)
                {
                    return ddosResult;
                }
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

            // 4. Check rate limit based on algorithm
            var result = options.Algorithm switch
            {
                RateLimitAlgorithm.TokenBucket => await CheckTokenBucketAsync(key, effectiveLimit, effectiveBurstCapacity, options),
                RateLimitAlgorithm.SlidingWindow => await CheckSlidingWindowAsync(key, effectiveLimit, options),
                RateLimitAlgorithm.LeakyBucket => await CheckLeakyBucketAsync(key, effectiveLimit, options),
                _ => await CheckFixedWindowAsync(key, effectiveLimit, options)
            };

            // 5. Domain-based rate limiting (if enabled)
            if (options.EnableDomainRateLimiting && result.IsAllowed)
            {
                var domainKey = GenerateDomainKey(context.Request.Host.Host, options);
                var domainResult = await CheckTokenBucketAsync(domainKey, options.DomainRequestsPerMinute, options.DomainBurstCapacity, options);
                
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

    private async Task<RateLimitResult> CheckDDoSProtectionAsync(string clientIp, RateLimitOptions options)
    {
        var ddosKey = $"ddos:{clientIp}";
        var window = _ddosWindows.GetOrAdd(ddosKey, _ => new SlidingWindow(options.WindowInSeconds));

        var now = DateTime.UtcNow;
        window.AddRequest(now);
        var requestCount = window.GetRequestCount(now);

        if (requestCount > options.DDoSThreshold)
        {
            // Check for emergency mode activation
            if (options.EmergencyMode.Enabled && requestCount > options.EmergencyMode.TriggerThreshold)
            {
                var emergencyBlockUntil = now.AddHours(options.EmergencyMode.BlockDurationHours);
                await BlockIpAsync(clientIp, emergencyBlockUntil);

                _logger.LogCritical("Emergency DDoS mode activated for {ClientIp}. Requests: {RequestCount}, Threshold: {Threshold}",
                    clientIp, requestCount, options.EmergencyMode.TriggerThreshold);

                return new RateLimitResult
                {
                    IsAllowed = false,
                    IsBlocked = true,
                    Key = ddosKey,
                    RemainingRequests = 0,
                    ResetTimeUtc = emergencyBlockUntil,
                    RetryAfterSeconds = (int)(emergencyBlockUntil - now).TotalSeconds,
                    BlockReason = "Emergency DDoS protection activated"
                };
            }

            // Progressive blocking based on severity
            var blockMinutes = options.DDoSBlockMinutes;
            if (options.ProgressiveBlocking)
            {
                var severity = (double)requestCount / options.DDoSThreshold;
                if (severity >= 3.0) blockMinutes *= 4;      // 4x block time for severe attacks
                else if (severity >= 2.0) blockMinutes *= 2; // 2x block time for moderate attacks
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

    private async Task<RateLimitResult> CheckTokenBucketAsync(string key, int limit, int burstCapacity, RateLimitOptions options)
    {
        var bucket = await _store.GetTokenBucketAsync(key);
        if (bucket == null)
        {
            bucket = new TokenBucket
            {
                Key = key,
                Capacity = burstCapacity,
                Tokens = burstCapacity,
                LastRefillTime = DateTime.UtcNow,
                RefillRate = (double)limit / options.WindowInSeconds
            };
        }

        var now = DateTime.UtcNow;
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

    private async Task<RateLimitResult> CheckFixedWindowAsync(string key, int limit, RateLimitOptions options)
    {
        var window = await _store.GetFixedWindowAsync(key);
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-options.WindowInSeconds);

        if (window == null || window.StartTime < windowStart)
        {
            window = new FixedWindow
            {
                Key = key,
                StartTime = now,
                Count = 0
            };
        }

        if (window.Count < limit)
        {
            window.Count++;
            await _store.SetFixedWindowAsync(window);
            
            return new RateLimitResult
            {
                IsAllowed = true,
                Key = key,
                CurrentCount = window.Count,
                Limit = limit,
                RemainingRequests = limit - window.Count,
                WindowRemainingSeconds = (int)(window.StartTime.AddSeconds(options.WindowInSeconds) - now).TotalSeconds,
                WindowResetTime = window.StartTime.AddSeconds(options.WindowInSeconds)
            };
        }

        return new RateLimitResult
        {
            IsAllowed = false,
            Key = key,
            CurrentCount = window.Count,
            Limit = limit,
            RemainingRequests = 0,
            WindowRemainingSeconds = (int)(window.StartTime.AddSeconds(options.WindowInSeconds) - now).TotalSeconds,
            WindowResetTime = window.StartTime.AddSeconds(options.WindowInSeconds)
        };
    }

    private async Task<RateLimitResult> CheckSlidingWindowAsync(string key, int limit, RateLimitOptions options)
    {
        var window = await _store.GetSlidingWindowAsync(key);
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-options.WindowInSeconds);

        if (window == null)
        {
            window = new SlidingWindow(options.WindowInSeconds);
        }

        // Remove old requests outside the window
        window.RemoveOldRequests(windowStart);
        
        if (window.RequestCount < limit)
        {
            window.AddRequest(now);
            await _store.SetSlidingWindowAsync(key, window);
            
            return new RateLimitResult
            {
                IsAllowed = true,
                Key = key,
                CurrentCount = window.RequestCount,
                Limit = limit,
                RemainingRequests = limit - window.RequestCount,
                WindowRemainingSeconds = options.WindowInSeconds,
                WindowResetTime = now.AddSeconds(options.WindowInSeconds)
            };
        }

        return new RateLimitResult
        {
            IsAllowed = false,
            Key = key,
            CurrentCount = window.RequestCount,
            Limit = limit,
            RemainingRequests = 0,
            WindowRemainingSeconds = options.WindowInSeconds,
            WindowResetTime = now.AddSeconds(options.WindowInSeconds)
        };
    }

    private async Task<RateLimitResult> CheckLeakyBucketAsync(string key, int limit, RateLimitOptions options)
    {
        var bucket = await _store.GetLeakyBucketAsync(key);
        var now = DateTime.UtcNow;

        if (bucket == null)
        {
            bucket = new LeakyBucket
            {
                Key = key,
                Capacity = limit,
                Level = 0,
                LastLeakTime = now,
                LeakRate = (double)limit / options.WindowInSeconds
            };
        }

        // Leak water from the bucket
        var timePassed = (now - bucket.LastLeakTime).TotalSeconds;
        var leakedAmount = timePassed * bucket.LeakRate;
        bucket.Level = Math.Max(0, bucket.Level - leakedAmount);
        bucket.LastLeakTime = now;

        if (bucket.Level < bucket.Capacity)
        {
            bucket.Level++;
            await _store.SetLeakyBucketAsync(bucket);
            
            return new RateLimitResult
            {
                IsAllowed = true,
                Key = key,
                CurrentCount = (long)bucket.Level,
                Limit = limit,
                RemainingRequests = limit - (long)bucket.Level,
                WindowRemainingSeconds = options.WindowInSeconds,
                WindowResetTime = now.AddSeconds(options.WindowInSeconds)
            };
        }

        return new RateLimitResult
        {
            IsAllowed = false,
            Key = key,
            CurrentCount = (long)bucket.Level,
            Limit = limit,
            RemainingRequests = 0,
            WindowRemainingSeconds = (int)((bucket.Level - bucket.Capacity) / bucket.LeakRate),
            WindowResetTime = now.AddSeconds((bucket.Level - bucket.Capacity) / bucket.LeakRate)
        };
    }

    private (int Limit, int BurstCapacity) CalculateAdaptiveLimits(RateLimitOptions options)
    {
        if (_currentServerLoad <= options.ServerLoadThreshold)
        {
            return (options.Limit, options.BurstCapacity);
        }

        // Reduce limits based on server load
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
                // Remove expired block
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
        // Check for forwarded IP first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0 && IPAddress.TryParse(ips[0].Trim(), out var ip))
            {
                return ip.ToString();
            }
        }

        // Check for real IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out var realIpAddress))
        {
            return realIpAddress.ToString();
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void MonitorServerLoad(object? state)
    {
        try
        {
            // Simple CPU-based load calculation
            // In production, this could be more sophisticated
            var process = System.Diagnostics.Process.GetCurrentProcess();
            _currentServerLoad = process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
            
            // Normalize to 0-1 range
            _currentServerLoad = Math.Min(1.0, _currentServerLoad);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error monitoring server load");
            _currentServerLoad = 0.0;
        }
    }

    /// <summary>
    /// Cleanup expired entries from DDoS detection and blocked IPs collections
    /// Prevents memory leaks by removing old entries
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var cutoffTime = now.AddHours(-1); // Remove entries older than 1 hour
            
            // Cleanup expired blocked IPs
            var expiredBlockedIps = _blockedIps
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var ip in expiredBlockedIps)
            {
                _blockedIps.TryRemove(ip, out _);
            }
            
            // Cleanup expired DDoS windows (remove windows with no recent activity)
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
        _cacheProvider?.Dispose();
    }
}
