using Microsoft.AspNetCore.Http;
using OElite.Restme.RateLimiting.Models;

namespace OElite.Restme.RateLimiting.Interfaces;

/// <summary>
/// Core rate limiting service interface
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Check if the current request is within the rate limit
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="options">Rate limit options</param>
    /// <returns>Rate limit result</returns>
    Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options);
}
