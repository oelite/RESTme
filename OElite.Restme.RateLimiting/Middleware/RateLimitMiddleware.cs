using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OElite.Restme.RateLimiting.Interfaces;
using OElite.Restme.RateLimiting.Models;
using System.Diagnostics;

namespace OElite.Restme.RateLimiting.Middleware;

/// <summary>
/// Production-grade rate limiting middleware
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly IRateLimitService _rateLimitService;
    private readonly IRateLimitKeyGenerator _keyGenerator;
    private readonly IRateLimitResponseBuilder _responseBuilder;
    private readonly RateLimitOptions _globalOptions;

    public RateLimitMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMiddleware> logger,
        IRateLimitService rateLimitService,
        IRateLimitKeyGenerator keyGenerator,
        IRateLimitResponseBuilder responseBuilder,
        IOptions<RateLimitOptions> globalOptions)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimitService = rateLimitService ?? throw new ArgumentNullException(nameof(rateLimitService));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
        _globalOptions = globalOptions?.Value ?? throw new ArgumentNullException(nameof(globalOptions));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting if disabled
        if (!_globalOptions.Enabled)
        {
            await _next(context);
            return;
        }

        // Check for endpoint-specific rate limit options
        var endpointOptions = GetEndpointOptions(context) ?? _globalOptions;

        if (!endpointOptions.Enabled)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check rate limit using the service
            var result = await _rateLimitService.CheckRateLimitAsync(context, endpointOptions);

            // Add rate limit headers if enabled
            if (endpointOptions.IncludeHeaders)
            {
                AddRateLimitHeaders(context, result, endpointOptions);
            }

            // Log rate limit check
            _logger.LogDebug("Rate limit check for key {Key}: {CurrentCount}/{Limit}, Allowed: {IsAllowed}",
                result.Key, result.CurrentCount, result.Limit, result.IsAllowed);

            if (!result.IsAllowed)
            {
                // Rate limit exceeded
                _logger.LogWarning("Rate limit exceeded for key {Key}: {CurrentCount}/{Limit}",
                    result.Key, result.CurrentCount, result.Limit);

                await _responseBuilder.BuildResponseAsync(context, result, endpointOptions, context.RequestAborted);
                return;
            }

            // Continue to next middleware
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected - exit gracefully without logging
            // TaskCanceledException inherits from OperationCanceledException, so this catches both
            // The 'when' filter ensures we only catch CLIENT-initiated cancellations, not server-side timeouts
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rate limiting middleware");

            // In case of error, allow the request to continue
            // This ensures rate limiting failures don't break the application
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Rate limit middleware executed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private RateLimitOptions? GetEndpointOptions(HttpContext context)
    {
        try
        {
            // Use reflection to safely get endpoint without direct dependency
            var features = context.Features;
            var endpointFeature = features.Get<dynamic>();

            if (endpointFeature?.Endpoint != null)
            {
                var endpoint = endpointFeature.Endpoint;
                var metadata = endpoint.Metadata;

                // Look for RateLimitAttribute in metadata
                foreach (var item in metadata)
                {
                    if (item is RateLimitAttribute rateLimitAttribute)
                    {
                        return new RateLimitOptions
                        {
                            Limit = rateLimitAttribute.Limit,
                            WindowInSeconds = rateLimitAttribute.WindowInSeconds,
                            KeyPrefix = rateLimitAttribute.KeyPrefix ?? _globalOptions.KeyPrefix,
                            Enabled = rateLimitAttribute.Enabled,
                            StatusCode = rateLimitAttribute.StatusCode,
                            ErrorMessage = rateLimitAttribute.ErrorMessage ?? _globalOptions.ErrorMessage,
                            IncludeHeaders = rateLimitAttribute.IncludeHeaders,
                            StorageType = _globalOptions.StorageType,
                            RedisConnectionString = _globalOptions.RedisConnectionString,
                            RedisKeyPrefix = _globalOptions.RedisKeyPrefix
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get endpoint metadata for rate limiting - using global options");
        }

        return null;
    }

    private static void AddRateLimitHeaders(HttpContext context, RateLimitResult result, RateLimitOptions options)
    {
        var headers = context.Response.Headers;

        // Standard rate limit headers
        headers["X-RateLimit-Limit"] = result.Limit.ToString();
        headers["X-RateLimit-Remaining"] = Math.Max(0, result.Limit - result.CurrentCount).ToString();
        headers["X-RateLimit-Reset"] = new DateTimeOffset(result.WindowResetTime).ToUnixTimeSeconds().ToString();
        headers["X-RateLimit-Reset-After"] = result.WindowRemainingSeconds.ToString();

        // Additional headers
        headers["X-RateLimit-Window"] = options.WindowInSeconds.ToString();
        headers["X-RateLimit-Policy"] = $"{result.Limit};w={options.WindowInSeconds}";

        // Retry-After header when rate limited
        if (!result.IsAllowed)
        {
            headers["Retry-After"] = result.WindowRemainingSeconds.ToString();
        }
    }
}

/// <summary>
/// Attribute for configuring rate limiting on specific endpoints
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RateLimitAttribute : Attribute
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
    /// The key prefix used to identify the rate limit bucket
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Whether to enable rate limiting for this endpoint
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The HTTP status code to return when rate limit is exceeded
    /// </summary>
    public int StatusCode { get; set; } = 429;

    /// <summary>
    /// Custom error message when rate limit is exceeded
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether to include rate limit headers in the response
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Constructor for rate limit attribute
    /// </summary>
    /// <param name="limit">Maximum requests per window</param>
    /// <param name="windowInSeconds">Time window in seconds</param>
    public RateLimitAttribute(int limit = 100, int windowInSeconds = 60)
    {
        Limit = limit;
        WindowInSeconds = windowInSeconds;
    }
}