using Microsoft.AspNetCore.Http;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using System.Text.Json;

namespace OElite.Restme.RateLimiting.Services;

/// <summary>
/// Default implementation of rate limit response builder
/// </summary>
public class DefaultRateLimitResponseBuilder : IRateLimitResponseBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task BuildResponseAsync(HttpContext context, RateLimitResult result, RateLimitOptions options, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = options.StatusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "rate_limit_exceeded",
            message = options.ErrorMessage,
            details = new
            {
                limit = result.Limit,
                current = result.CurrentCount,
                remaining = Math.Max(0, result.Limit - result.CurrentCount),
                resetTime = new DateTimeOffset(result.WindowResetTime).ToUnixTimeSeconds(),
                retryAfter = result.WindowRemainingSeconds
            }
        };

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
        await context.Response.WriteAsync(json, cancellationToken);
    }
}