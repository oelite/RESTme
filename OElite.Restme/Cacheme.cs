using System;
using System.Diagnostics;
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

    public static async Task<bool> ExpiremeAsync(this Rest rest, string? uid, bool invalidateGracePeriod = true)
    {
        if (rest.CurrentMode != RestMode.RedisAsCache)
            throw new OEliteException("Cacheme currently only support Redis mode");
        
        if (rest.CacheProvider == null)
            throw new OEliteException("Cache provider not initialized");
            
        var obj = await rest.CacheProvider.GetAsync<ResponseMessage>(uid);
        if (obj is not { Data: not null }) return false;
        
        if (invalidateGracePeriod)
        {
            await rest.CacheProvider.RemoveAsync(uid);
        }
        else
        {
            obj.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
            if (invalidateGracePeriod)
            {
                obj.GraceTillUtc = obj.ExpiryOnUtc;
            }

            await rest.CacheProvider.SetAsync(uid, obj);
        }

        return true;
    }

    public static async Task<ResponseMessage?> CachemeAsync(this Rest? rest, string? uid, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        if (rest?.CurrentMode != RestMode.RedisAsCache)
            throw new OEliteException("Cacheme currently only support Redis mode");
        
        if (rest.CacheProvider == null)
            throw new OEliteException("Cache provider not initialized");
            
        if (!uid.IsNotNullOrEmpty())
        {
            rest?.LogInfo($"[CACHE-Cacheme]: Invalid uid provided");
            return null;
        }

        var expiry = expiryInSeconds > 0
            ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
            : DateTime.UtcNow.AddSeconds(DefaultCacheExpiryInSeconds);
        var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;
        var graceInMinutes = (grace - DateTime.UtcNow).Minutes;

        try
        {
            rest.LogInfo(
                $"[CACHE-Cacheme]: Caching data for [{(data.GetType().IsClass ? data.GetType().Name : data.ToString())}] {uid}");
            var responseMessage = new ResponseMessage(data)
            {
                ExpiryOnUtc = expiry,
                GraceTillUtc = grace
            };
            
            var expiryTimeSpan = graceInMinutes > 0 ? (TimeSpan?)TimeSpan.FromMinutes(graceInMinutes) : null;
            var success = await rest.CacheProvider.SetAsync(uid, responseMessage, expiryTimeSpan);
            var result = success ? responseMessage : null;

            rest.LogInfo(result != null
                ? $"[CACHE-Cacheme]: Successfully cached data for  [{(data.GetType().IsClass ? data.GetType().Name : data.ToString())}] {uid}"
                : $"[CACHE-Cacheme]: Failed to cache data for [{(data.GetType().IsClass ? data.GetType().Name : data.ToString())}] {uid}");

            return result;
        }
        catch (Exception? ex)
        {
            rest.LogError(
                $"[CACHE-Cacheme]: Error caching data for [{(data.GetType().IsClass ? data.GetType().Name : data.ToString())}] {uid}: {ex.Message}",
                ex);
            return null;
        }
    }

    public static async Task<T?> FindmeAsync<T>(this Rest? rest, string? uid, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null) where T : class?

    {
        if (rest?.CurrentMode != RestMode.RedisAsCache)
            throw new OEliteException("Cacheme currently only support Redis mode");
        
        if (rest.CacheProvider == null)
            throw new OEliteException("Cache provider not initialized");
            
        var sw = Stopwatch.StartNew();
        var obj = await rest.CacheProvider.GetAsync<ResponseMessage>(uid);
        sw.Stop();
        rest.LogInfo($"[CACHE-Findme]: Retrieved cache in {sw.ElapsedMilliseconds}ms for [{typeof(T).Name}] {uid}");
        if (obj is null)
        {
            rest.LogInfo($"[CACHE-Findme]: Missed cache for [{typeof(T).Name}] {uid}");
            if (refreshAction == null) return null;
            rest.LogInfo($"[CACHE-Findme]: Refresh activated for [{typeof(T).Name}] {uid}");
            return await refreshAction();
        }

        var result = rest.CacheProvider.GetOriginalData<T>(obj);

        var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result);

        if (!customValidationResult)
        {
            rest.LogInfo($"[CACHE-Findme]: Validation failed for [{typeof(T).Name}] {uid}");
            if (refreshAction == null) return null;
            rest.LogInfo($"[CACHE-Findme]: Refresh activated for [{typeof(T).Name}] {uid}");
            return await refreshAction();
        }

        if (returnExpired)
        {
            rest.LogInfo($"[CACHE-Findme]: Return expired cache for [{typeof(T).Name}] {uid}");
            return result;
        }

        if (returnInGrace && obj.GraceTillUtc >= DateTime.UtcNow)
        {
            if (obj.ExpiryOnUtc <= DateTime.UtcNow && refreshAction != null)
            {
                rest.LogInfo($"[CACHE-Findme]: Refresh activated for [{typeof(T).Name}] {uid}");
                refreshAction().RunInBackgroundAndForget();
            }

            rest.LogInfo($"[CACHE-Findme]: Return expired but stale cache for [{typeof(T).Name}] {uid}");
            return result;
        }

        if (obj.ExpiryOnUtc >= DateTime.UtcNow) return result;

        return refreshAction != null ? await refreshAction() : null;
    }


    public static Task<T?> FindmeAsync<T>(this Rest rest, object queryObject, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null) where T : class?
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var sw = Stopwatch.StartNew();
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
        sw.Stop();
        rest.LogInfo($"[CACHE-Findme]: MD5 cache key used {sw.ElapsedMilliseconds}ms for [{typeof(T).Name}] {md5}");

        return rest.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction);
    }

    public static Task<ResponseMessage?> CachemeAsync(this Rest rest, object queryObject, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize(serializeInResponseMessageWrapper: false);
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{data.GetType().Name}:{json}");
        return rest.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds);
    }

    public static Task<bool> ExpiremeAsync<T>(this Rest rest, object queryObject, bool invalidateGracePeriod = true)
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
        return rest.ExpiremeAsync(md5, invalidateGracePeriod);
    }
}