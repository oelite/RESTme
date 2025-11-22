using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OElite.Restme;
using OElite.Restme.Abstractions;

// ReSharper disable once CheckNamespace
namespace OElite;

/// <summary>
/// Caching Utils for ICacheProvider
/// </summary>
public static class CacheProviderExtensions
{
    /// <summary>
    /// default expiry in seconds for cached item
    /// </summary>
    private const int DefaultCacheExpiryInSeconds = 60;

    public static async Task<bool> ExpiremeAsync(this ICacheProvider cacheProvider, string? uid, bool invalidateGracePeriod = true, CancellationToken cancellationToken = default)
    {
        var obj = await cacheProvider.GetAsync<ResponseMessage>(uid, cancellationToken);
        if (obj is not { Data: not null }) return false;

        if (invalidateGracePeriod)
        {
            await cacheProvider.RemoveAsync(uid, cancellationToken);
        }
        else
        {
            obj.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
            if (invalidateGracePeriod)
            {
                obj.GraceTillUtc = obj.ExpiryOnUtc;
            }

            await cacheProvider.SetAsync(uid, obj, cancellationToken: cancellationToken);
        }

        return true;
    }


    public static async Task<ResponseMessage?> CachemeAsync(this ICacheProvider cacheProvider, string? uid, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1, CancellationToken cancellationToken = default)
    {
        if (!uid.IsNotNullOrEmpty())
        {
            return null;
        }

        var expiry = expiryInSeconds > 0
            ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
            : DateTime.UtcNow.AddSeconds(DefaultCacheExpiryInSeconds);
        var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;
        var graceInMinutes = (grace - DateTime.UtcNow).Minutes;

        try
        {
            var responseMessage = new ResponseMessage(data)
            {
                ExpiryOnUtc = expiry,
                GraceTillUtc = grace
            };

            var expiryTimeSpan = graceInMinutes > 0 ? (TimeSpan?)TimeSpan.FromMinutes(graceInMinutes) : null;
            var success = await cacheProvider.SetAsync(uid, responseMessage, expiryTimeSpan, cancellationToken);
            var result = success ? responseMessage : null;

            return result;
        }
        catch (Exception? ex)
        {
            return null;
        }
    }

    public static async Task<T?> FindmeAsync<T>(this ICacheProvider cacheProvider, string? uid, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null, CancellationToken cancellationToken = default) where T : class?
    {
        var obj = await cacheProvider.GetAsync<ResponseMessage>(uid, cancellationToken);
        if (obj is null)
        {
            if (refreshAction == null) return null;
            return await refreshAction();
        }

        var result = cacheProvider.GetOriginalData<T>(obj);

        var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result);

        if (!customValidationResult)
        {
            if (refreshAction == null) return null;
            return await refreshAction();
        }

        if (returnExpired)
        {
            return result;
        }

        if (returnInGrace && obj.GraceTillUtc >= DateTime.UtcNow)
        {
            if (obj.ExpiryOnUtc <= DateTime.UtcNow && refreshAction != null)
            {
                refreshAction().RunInBackgroundAndForget();
            }

            return result;
        }

        if (obj.ExpiryOnUtc >= DateTime.UtcNow) return result;

        return refreshAction != null ? await refreshAction() : null;
    }


    public static Task<T?> FindmeAsync<T>(this ICacheProvider cacheProvider, object queryObject, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null) where T : class?
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

        return cacheProvider.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction);
    }

    public static Task<ResponseMessage?> CachemeAsync(this ICacheProvider cacheProvider, object queryObject, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{data.GetType().Name}:{json}");
        return cacheProvider.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds);
    }

    public static Task<bool> ExpiremeAsync<T>(this ICacheProvider cacheProvider, object queryObject, bool invalidateGracePeriod = true)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
        return cacheProvider.ExpiremeAsync(md5, invalidateGracePeriod);
    }
}