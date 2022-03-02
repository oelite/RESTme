using System;
using System.Threading.Tasks;

namespace OElite;

/// <summary>
/// Caching Utils for Restme
/// </summary>
public static class RestmeCacheExtensions
{
    /// <summary>
    /// default expiry in seconds for cached item
    /// </summary>
    public const int defaultCacheExpiryInSeconds = 60;

    /// <summary>
    /// default expiry in seconds as cache grace period
    /// </summary>
    public const int defaultCacheGraceInSeconds = 3600;

    /// <summary>
    /// default refresh in seconds based on a cache's cachedOnUtc
    /// </summary>
    public const int defaultCacheRefreshInSeconds = 60;

    public static async Task<ResponseMessage> Cacheme(this Rest rest, string uid, object data, int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        var result = default(ResponseMessage);
        if (rest == null || !uid.IsNotNullOrEmpty()) return result;

        var expiry = expiryInSeconds > 0
            ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
            : DateTime.UtcNow.AddSeconds(defaultCacheExpiryInSeconds);
        var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;

        var responseMessage = new ResponseMessage(data)
        {
            ExpiryOnUtc = expiry,
            GraceTillUtc = grace
        };
        result = await rest?.PostAsync<ResponseMessage>(uid, responseMessage);

        return result;
    }

    public static async Task<T> Findme<T>(this Rest rest, string uid, bool returnExpired = false,
        bool returnInGrace = true) where T : class
    {
        var obj = rest?.Get<ResponseMessage>(uid);
        if (obj is { Data: { } })
        {
            var result = obj as T;
            if (returnExpired) return result;
            if (returnInGrace && obj.GraceTillUtc >= DateTime.UtcNow) return result;
            if (obj.ExpiryOnUtc >= DateTime.UtcNow) return result;
        }

        return default;
    }
}