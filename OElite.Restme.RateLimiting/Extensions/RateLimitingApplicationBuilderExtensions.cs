using Microsoft.AspNetCore.Builder;
using OElite.Restme.RateLimiting.Middleware;

namespace OElite.Restme.RateLimiting.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting middleware
/// </summary>
public static class RateLimitingApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the rate limiting middleware to the specified IApplicationBuilder
    /// </summary>
    /// <param name="app">The IApplicationBuilder to add the middleware to</param>
    /// <returns>The IApplicationBuilder so that additional calls can be chained</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitMiddleware>();
    }
}