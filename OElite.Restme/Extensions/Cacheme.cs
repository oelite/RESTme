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
        if (string.IsNullOrEmpty(uid))
            return false;

        // Check if the key exists
        var exists = await cacheProvider.ExistsAsync(uid, cancellationToken);
        if (!exists)
            return false;

        // With direct data storage, we simply remove the cache entry
        // The gracePeriod parameter is ignored since we no longer have ResponseMessage wrapper
        await cacheProvider.RemoveAsync(uid, cancellationToken);
        return true;
    }


    public static async Task<bool> CachemeAsync<T>(this ICacheProvider cacheProvider, string? uid, T data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1, CancellationToken cancellationToken = default) where T : class
    {
        if (!uid.IsNotNullOrEmpty() || data == null)
        {
            return false;
        }

        try
        {
            var expiry = expiryInSeconds > 0
                ? TimeSpan.FromSeconds(expiryInSeconds)
                : TimeSpan.FromSeconds(DefaultCacheExpiryInSeconds);

            // Note: graceInSeconds is ignored in direct data storage - provider-level expiry handles this
            var success = await cacheProvider.SetAsync(uid, data, expiry, cancellationToken);
            return success;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public static async Task<T?> FindmeAsync<T>(this ICacheProvider cacheProvider, string? uid, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null, CancellationToken cancellationToken = default) where T : class?
    {
        if (string.IsNullOrEmpty(uid))
        {
            return refreshAction != null ? await refreshAction() : null;
        }

        var result = await cacheProvider.GetAsync<T>(uid, cancellationToken);
        if (result is null)
        {
            return refreshAction != null ? await refreshAction() : null;
        }

        // Run additional validation if provided
        var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result);
        if (!customValidationResult)
        {
            return refreshAction != null ? await refreshAction() : null;
        }

        // With direct data storage, provider-level expiry validation handles expiry checks
        // returnExpired and returnInGrace parameters are simplified since we don't have ResponseMessage wrapper
        return result;
    }


    public static Task<T?> FindmeAsync<T>(this ICacheProvider cacheProvider, object queryObject, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null, CancellationToken cancellationToken = default) where T : class?
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

        return cacheProvider.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction, cancellationToken);
    }

    public static Task<bool> CachemeAsync<T>(this ICacheProvider cacheProvider, object queryObject, T data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1, CancellationToken cancellationToken = default) where T : class
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        if (data == null) throw new ArgumentNullException(nameof(data));
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{data.GetType().Name}:{json}");
        return cacheProvider.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds, cancellationToken);
    }

    public static Task<bool> ExpiremeAsync<T>(this ICacheProvider cacheProvider, object queryObject, bool invalidateGracePeriod = true, CancellationToken cancellationToken = default)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = StringUtils.JsonSerialize(queryObject);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
        return cacheProvider.ExpiremeAsync(md5, invalidateGracePeriod, cancellationToken);
    }
}