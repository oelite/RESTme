using Microsoft.AspNetCore.Http;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using System.Net;

namespace OElite.Restme.RateLimiting.Services;

/// <summary>
/// Default implementation of rate limit key generator
/// </summary>
public class DefaultRateLimitKeyGenerator : IRateLimitKeyGenerator
{
    public string GenerateKey(HttpContext context, RateLimitOptions options)
    {
        var keyPrefix = options.KeyPrefix ?? "default";
        var identifier = GetClientIdentifier(context);
        var endpoint = GetEndpointIdentifier(context);

        return $"{keyPrefix}:{identifier}:{endpoint}";
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID first (if authenticated)
        var userId = context.User?.FindFirst("sub")?.Value
                    ?? context.User?.FindFirst("user_id")?.Value
                    ?? context.User?.Identity?.Name;

        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = GetClientIpAddress(context);
        return $"ip:{ipAddress}";
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP headers (reverse proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GetEndpointIdentifier(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        // Normalize path by removing query parameters and trailing slashes
        path = path.Split('?')[0].TrimEnd('/');
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        return $"{method}:{path}";
    }
}