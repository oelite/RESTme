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
}