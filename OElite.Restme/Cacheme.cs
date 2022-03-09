using System;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace OElite;

/// <summary>
/// Caching Utils for Restme
/// </summary>
public static class RestmeCacheExtensions
{
    /// <summary>
    /// default expiry in seconds for cached item
    /// </summary>
    private const int DefaultCacheExpiryInSeconds = 60;

    public static async Task<bool> ExpiremeAsync(this Rest rest, string uid, bool invalidateGracePeriod = true)
    {
        if (rest?.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        var obj = rest?.Get<ResponseMessage>(uid);
        if (obj is { Data: { } })
        {
            if (invalidateGracePeriod)
            {
                await rest?.DeleteAsync<ResponseMessage>(uid);
            }
            else
            {
                obj.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
                if (invalidateGracePeriod)
                {
                    obj.GraceTillUtc = obj.ExpiryOnUtc;
                }

                await rest?.PostAsync<ResponseMessage>(uid, obj);
            }

            return true;
        }

        return false;
    }

    public static async Task<ResponseMessage> CachemeAsync(this Rest rest, string uid, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        if (rest?.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        var result = default(ResponseMessage);
        if (rest == null || !uid.IsNotNullOrEmpty()) return result;

        var expiry = expiryInSeconds > 0
            ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
            : DateTime.UtcNow.AddSeconds(DefaultCacheExpiryInSeconds);
        var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;

        var responseMessage = new ResponseMessage(data)
        {
            ExpiryOnUtc = expiry,
            GraceTillUtc = grace
        };
        result = await rest?.PostAsync<ResponseMessage>(uid, responseMessage);

        return result;
    }

    public static async Task<T> FindmeAsync<T>(this Rest rest, string uid, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>> additionalValidation = null,
        Func<Task<T>> refreshAction = null) where T : class

    {
        if (rest?.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        var obj = rest?.Get<ResponseMessage>(uid);
        if (obj is {Data: { }})
        {
            var result = obj.GetOriginalData<T>();

            var customValidationResult = (additionalValidation == null || await additionalValidation.Invoke(result));

            if (customValidationResult)
            {
                if (returnExpired) return result;
                if (returnInGrace && obj.GraceTillUtc >= DateTime.UtcNow)
                {
                    if (obj.ExpiryOnUtc <= DateTime.UtcNow)
                    {
                        refreshAction.Invoke().RunInBackgroundAndForget();
                    }

                    return result;
                }

                if (obj.ExpiryOnUtc >= DateTime.UtcNow) return result;
            }
        }

        return await refreshAction.Invoke();
    }
}