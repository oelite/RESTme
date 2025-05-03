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
        if (!uid.IsNotNullOrEmpty()) return null;

        var expiry = expiryInSeconds > 0
            ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
            : DateTime.UtcNow.AddSeconds(DefaultCacheExpiryInSeconds);
        var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;
        var graceInMinutes = (grace - DateTime.UtcNow).Minutes;

        var responseMessage = new ResponseMessage(data)
        {
            ExpiryOnUtc = expiry,
            GraceTillUtc = grace
        };
        var result = await rest?.PostAsync<ResponseMessage>(uid, responseMessage,
            graceInMinutes > 0 ? TimeSpan.FromMinutes(graceInMinutes) : null);

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
        if (obj is { Data: { } })
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


    public static Task<T> FindmeAsync<T>(this Rest rest, object queryObject, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>> additionalValidation = null,
        Func<Task<T>> refreshAction = null) where T : class
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.MD5Encrypt(queryObject.JsonSerialize());
        return rest.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction);
    }

    public static Task<ResponseMessage> CachemeAsync(this Rest rest, object queryObject, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.MD5Encrypt(queryObject.JsonSerialize());
        return rest.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds);
    }

    public static Task<bool> ExpiremeAsync(this Rest rest, object queryObject, bool invalidateGracePeriod = true)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.MD5Encrypt(queryObject.JsonSerialize());

        return rest.ExpiremeAsync(md5, invalidateGracePeriod);
    }
}