using System;
using System.Runtime.CompilerServices;
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
        if (rest.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        var obj = rest.Get<ResponseMessage>(uid);
        if (obj is not { Data: not null }) return false;
        if (invalidateGracePeriod)
        {
            await rest.DeleteAsync<ResponseMessage>(uid);
        }
        else
        {
            obj.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
            if (invalidateGracePeriod)
            {
                obj.GraceTillUtc = obj.ExpiryOnUtc;
            }

            await rest.PostAsync<ResponseMessage>(uid, obj);
        }

        return true;
    }

    public static async Task<ResponseMessage?> CachemeAsync(this Rest? rest, string? uid, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1, [CallerMemberName] string callerName = "")
    {
        if (rest?.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        if (!uid.IsNotNullOrEmpty()) return null;

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
            var result = await rest.PostAsync<ResponseMessage>(uid, responseMessage,
                graceInMinutes > 0 ? TimeSpan.FromMinutes(graceInMinutes) : null);

            return result;
        }
        catch (Exception? ex)
        {
            rest.LogError(ex.Message, ex);
            return null;
        }
    }

    public static async Task<T?> FindmeAsync<T>(this Rest? rest, string? uid, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null) where T : class

    {
        if (rest?.CurrentMode != RestMode.RedisCacheClient)
            throw new OEliteException("Cacheme currently only support Redis mode");
        var obj = rest.Get<ResponseMessage>(uid);
        if (obj is null) return refreshAction != null ? await refreshAction.Invoke() : null;
        var result = obj.GetOriginalData<T>();

        var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result);

        if (!customValidationResult) return await refreshAction?.Invoke()!;
        if (returnExpired) return result;
        if (returnInGrace && obj.GraceTillUtc >= DateTime.UtcNow)
        {
            if (obj.ExpiryOnUtc <= DateTime.UtcNow)
            {
                refreshAction?.Invoke().RunInBackgroundAndForget();
            }

            return result;
        }

        if (obj.ExpiryOnUtc >= DateTime.UtcNow) return result;

        return refreshAction != null ? await refreshAction.Invoke() : null;
    }


    public static Task<T?> FindmeAsync<T>(this Rest rest, object queryObject, bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null,
        [CallerMemberName] string callerName = "") where T : class
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{callerName}:{json}");
        return rest.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction);
    }

    public static Task<ResponseMessage?> CachemeAsync(this Rest rest, object queryObject, object data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1,
        [CallerMemberName] string callerName = "")
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{callerName}:{json}");
        return rest.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds);
    }

    public static Task<bool> ExpiremeAsync(this Rest rest, object queryObject, bool invalidateGracePeriod = true,
        [CallerMemberName] string callerName = "")
    {
        if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));
        var json = queryObject.JsonSerialize();
        if (json.IsNullOrEmpty()) throw new OEliteException("No valid query object identified");
        var md5 = EncryptHelper.Md5Encrypt($"{callerName}:{json}");
        return rest.ExpiremeAsync(md5, invalidateGracePeriod);
    }
}