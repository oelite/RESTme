namespace OElite.Restme.RateLimiting.Models;

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// The maximum number of requests allowed within the specified time window
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// The time window in seconds
    /// </summary>
    public int WindowInSeconds { get; set; } = 60;

    /// <summary>
    /// The key used to identify the rate limit bucket (e.g., IP address, user ID)
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Whether to enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The HTTP status code to return when rate limit is exceeded
    /// </summary>
    public int StatusCode { get; set; } = 429;

    /// <summary>
    /// Custom error message when rate limit is exceeded
    /// </summary>
    public string ErrorMessage { get; set; } = "Rate limit exceeded. Too many requests.";

    /// <summary>
    /// Whether to include rate limit headers in the response
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Storage type for rate limiting data
    /// </summary>
    public RateLimitStorageType StorageType { get; set; } = RateLimitStorageType.Memory;

    /// <summary>
    /// Redis connection string (required when StorageType is Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Redis key prefix for rate limiting data
    /// </summary>
    public string RedisKeyPrefix { get; set; } = "rate_limit:";

    // === ADVANCED FEATURES FROM KORTEX ===

    /// <summary>
    /// Burst capacity for token bucket algorithm (allows short bursts above normal rate)
    /// </summary>
    public int BurstCapacity { get; set; } = 200;

    /// <summary>
    /// Enable DDoS protection with sliding window detection
    /// </summary>
    public bool EnableDDoSProtection { get; set; } = true;

    /// <summary>
    /// DDoS detection threshold (requests per window)
    /// </summary>
    public int DDoSThreshold { get; set; } = 1000;

    /// <summary>
    /// DDoS blocking duration in minutes
    /// </summary>
    public int DDoSBlockMinutes { get; set; } = 60;

    /// <summary>
    /// Enable progressive blocking (escalating block duration based on severity)
    /// </summary>
    public bool ProgressiveBlocking { get; set; } = true;

    /// <summary>
    /// Enable adaptive rate limiting based on server load
    /// </summary>
    public bool EnableAdaptiveLimiting { get; set; } = false;

    /// <summary>
    /// Server load threshold for adaptive limiting (0.0 - 1.0)
    /// </summary>
    public double ServerLoadThreshold { get; set; } = 0.6;

    /// <summary>
    /// Emergency mode configuration for severe attacks
    /// </summary>
    public EmergencyModeConfig EmergencyMode { get; set; } = new();

    /// <summary>
    /// Domain-based rate limiting (separate from IP-based)
    /// </summary>
    public bool EnableDomainRateLimiting { get; set; } = false;

    /// <summary>
    /// Domain rate limit (requests per minute)
    /// </summary>
    public int DomainRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Domain burst capacity
    /// </summary>
    public int DomainBurstCapacity { get; set; } = 2000;

    /// <summary>
    /// Rate limiting algorithm to use
    /// </summary>
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;
}

/// <summary>
/// Storage types for rate limiting data
/// </summary>
public enum RateLimitStorageType
{
    /// <summary>
    /// In-memory storage (not suitable for distributed scenarios)
    /// </summary>
    Memory,

    /// <summary>
    /// Redis distributed storage
    /// </summary>
    Redis
}

/// <summary>
/// Rate limit result containing current status
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Current request count in the time window
    /// </summary>
    public long CurrentCount { get; set; }

    /// <summary>
    /// Maximum allowed requests in the time window
    /// </summary>
    public long Limit { get; set; }

    /// <summary>
    /// Time remaining in the current window (in seconds)
    /// </summary>
    public long WindowRemainingSeconds { get; set; }

    /// <summary>
    /// Time when the window resets (UTC)
    /// </summary>
    public DateTime WindowResetTime { get; set; }

    /// <summary>
    /// The key used for this rate limit check
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Whether the request was blocked due to DDoS protection
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Remaining requests in current window
    /// </summary>
    public long RemainingRequests { get; set; }

    /// <summary>
    /// Retry after seconds (for blocked requests)
    /// </summary>
    public int RetryAfterSeconds { get; set; }

    /// <summary>
    /// Block reason (for blocked requests)
    /// </summary>
    public string BlockReason { get; set; } = string.Empty;

    /// <summary>
    /// Reset time for blocked requests
    /// </summary>
    public DateTime ResetTimeUtc { get; set; }
}

/// <summary>
/// Emergency mode configuration for severe DDoS attacks
/// </summary>
public class EmergencyModeConfig
{
    /// <summary>
    /// Whether emergency mode is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Request threshold to trigger emergency mode
    /// </summary>
    public int TriggerThreshold { get; set; } = 5000;

    /// <summary>
    /// Block duration in hours for emergency mode
    /// </summary>
    public int BlockDurationHours { get; set; } = 24;

    /// <summary>
    /// Whether to send alerts when emergency mode is triggered
    /// </summary>
    public bool SendAlerts { get; set; } = true;
}

/// <summary>
/// Rate limiting algorithms
/// </summary>
public enum RateLimitAlgorithm
{
    /// <summary>
    /// Fixed window algorithm
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Token bucket algorithm (allows bursts)
    /// </summary>
    TokenBucket,

    /// <summary>
    /// Sliding window algorithm
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// Leaky bucket algorithm
    /// </summary>
    LeakyBucket
}