using OElite.Restme.RateLimiting.Models;

namespace OElite.Restme.RateLimiting.Interfaces;

/// <summary>
/// Interface for rate limit storage implementations
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Check and increment the rate limit for a given key
    /// </summary>
    /// <param name="key">The rate limit key</param>
    /// <param name="options">Rate limit options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit result</returns>
    Task<RateLimitResult> CheckAndIncrementAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current rate limit status for a given key
    /// </summary>
    /// <param name="key">The rate limit key</param>
    /// <param name="options">Rate limit options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit result</returns>
    Task<RateLimitResult> GetStatusAsync(string key, RateLimitOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the rate limit for a given key
    /// </summary>
    /// <param name="key">The rate limit key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the key was cleared, false if it didn't exist</returns>
    Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all rate limit data (use with caution)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of keys cleared</returns>
    Task<long> ClearAllAsync(CancellationToken cancellationToken = default);
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