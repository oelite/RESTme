using OElite.Restme.RateLimiting.Models;

namespace OElite.Restme.RateLimiting.Interfaces;

/// <summary>
/// Interface for rate limit storage implementations
/// </summary>
public interface IRateLimitStore
{
    // Token Bucket methods
    Task<TokenBucket?> GetTokenBucketAsync(string key);
    Task SetTokenBucketAsync(TokenBucket bucket);

    // Fixed Window methods
    Task<FixedWindow?> GetFixedWindowAsync(string key);
    Task SetFixedWindowAsync(FixedWindow window);

    // Sliding Window methods
    Task<SlidingWindow?> GetSlidingWindowAsync(string key);
    Task SetSlidingWindowAsync(string key, SlidingWindow window);

    // Leaky Bucket methods
    Task<LeakyBucket?> GetLeakyBucketAsync(string key);
    Task SetLeakyBucketAsync(LeakyBucket bucket);
}

/// <summary>
/// Extended interface for high-performance atomic operations
/// </summary>
public interface IAtomicRateLimitStore : IRateLimitStore
{
    // Atomic operations for Redis - single round-trip with Lua scripts
    Task<(bool allowed, double tokens, double used)> CheckAndUpdateTokenBucketAsync(string key, int capacity, double refillRate, int windowSeconds);
    Task<(bool allowed, long count, long remaining)> CheckAndUpdateFixedWindowAsync(string key, int limit, int windowSeconds);
    Task<(bool allowed, long count, long remaining)> CheckAndUpdateSlidingWindowAsync(string key, int limit, int windowSeconds);
    Task<(bool allowed, double level, double remaining)> CheckAndUpdateLeakyBucketAsync(string key, int capacity, double leakRate, int windowSeconds);
}

/// <summary>
/// Interface for rate limit key generators
/// </summary>
public interface IRateLimitKeyGenerator
{
    /// <summary>
    /// Generate a rate limit key for the current HTTP context
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="options">Rate limit options</param>
    /// <returns>Rate limit key</returns>
    string GenerateKey(Microsoft.AspNetCore.Http.HttpContext context, RateLimitOptions options);
}

/// <summary>
/// Interface for rate limit response builders
/// </summary>
public interface IRateLimitResponseBuilder
{
    /// <summary>
    /// Build the HTTP response when rate limit is exceeded
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="result">Rate limit result</param>
    /// <param name="options">Rate limit options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the response building operation</returns>
    Task BuildResponseAsync(Microsoft.AspNetCore.Http.HttpContext context, RateLimitResult result, RateLimitOptions options, CancellationToken cancellationToken = default);
}