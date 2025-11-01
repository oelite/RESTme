using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace OElite.Restme.Hosting.Extensions
{
    public static class MemoryCacheExtensions
    {
        private const int DefaultCacheExpiryInSeconds = 60;

        private static async Task<T?> FindmeAsync<T>(this IMemoryCache cache,
            string key,
            bool returnExpired = false,
            bool returnInGrace = true,
            Func<T, CancellationToken, Task<bool>>? additionalValidation = null,
            Func<CancellationToken, Task<T>>? refreshAction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var sw = Stopwatch.StartNew();
            var found = cache.TryGetValue(key, out var cachedValue);
            sw.Stop();

            if (!found || cachedValue == null)
            {
                if (refreshAction == null) return null;
                return await refreshAction(cancellationToken);
            }

            try
            {
                ResponseMessage? responseMessage = null;

                if (cachedValue is ResponseMessage rm)
                {
                    responseMessage = rm;
                }
                else if (cachedValue is string jsonString)
                {
                    responseMessage = JsonSerializer.Deserialize<ResponseMessage>(jsonString);
                }
                else
                {
                    if (refreshAction == null) return null;
                    return await refreshAction(cancellationToken);
                }

                if (responseMessage?.Data == null) return null;

                T? result = null;
                if (responseMessage.Data is T directResult)
                {
                    result = directResult;
                }
                else if (responseMessage.Data is JsonElement jsonElement)
                {
                    result = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                else
                {
                    result = JsonSerializer.Deserialize<T>(responseMessage.Data.ToString()!);
                }

                if (result == null) return null;

                var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result, cancellationToken);
                if (!customValidationResult)
                {
                    if (refreshAction == null) return null;
                    return await refreshAction(cancellationToken);
                }

                if (returnExpired) return result;

                if (returnInGrace && responseMessage.GraceTillUtc >= DateTime.UtcNow)
                {
                    if (responseMessage.ExpiryOnUtc <= DateTime.UtcNow && refreshAction != null)
                    {
                        _ = Task.Run(async () => await refreshAction(cancellationToken), cancellationToken);
                    }

                    return result;
                }

                if (responseMessage.ExpiryOnUtc >= DateTime.UtcNow) return result;

                return refreshAction != null ? await refreshAction(cancellationToken) : null;
            }
            catch (JsonException)
            {
                if (refreshAction == null) return null;
                return await refreshAction(cancellationToken);
            }
        }

        public static async Task<T?> FindmeAsync<T>(this IMemoryCache cache,
            object queryObject,
            bool returnExpired = false,
            bool returnInGrace = true,
            Func<T, CancellationToken, Task<bool>>? additionalValidation = null,
            Func<CancellationToken, Task<T>>? refreshAction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var json = JsonSerializer.Serialize(queryObject);
            var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

            return await cache.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction,
                cancellationToken);
        }

        public static Task CachemeAsync<T>(this IMemoryCache cache,
            string key,
            T data,
            int expiryInSeconds = -1,
            int graceInSeconds = -1) where T : class
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!typeof(Rest).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException("CachemeAsync only supports Rest implementation types");

            var expiry = expiryInSeconds > 0
                ? DateTime.UtcNow.AddSeconds(expiryInSeconds)
                : DateTime.UtcNow.AddSeconds(DefaultCacheExpiryInSeconds);
            var grace = graceInSeconds > 0 ? expiry.AddSeconds(graceInSeconds) : expiry;

            var responseMessage = new ResponseMessage(data)
            {
                ExpiryOnUtc = expiry,
                GraceTillUtc = grace
            };

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = grace - DateTime.UtcNow
            };

            cache.Set(key, responseMessage, options);
            return Task.CompletedTask;
        }

        public static void CachemeAsync<T>(this IMemoryCache cache,
            object queryObject,
            T data,
            int expiryInSeconds = -1,
            int graceInSeconds = -1) where T : class
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var key = queryObject is string strKey ? strKey : null;
            if (key.IsNullOrEmpty())
            {
                var json = JsonSerializer.Serialize(queryObject);
                key = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
            }

            cache.CachemeAsync(key!, data, expiryInSeconds, graceInSeconds);
        }

        public static bool ExpiremeAsync(this IMemoryCache cache,
            string key,
            bool invalidateGracePeriod = true)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var found = cache.TryGetValue(key, out var cachedValue);
            if (!found || cachedValue == null) return false;

            try
            {
                ResponseMessage? responseMessage = null;

                if (cachedValue is ResponseMessage rm)
                {
                    responseMessage = rm;
                }
                else if (cachedValue is string jsonString)
                {
                    responseMessage = JsonSerializer.Deserialize<ResponseMessage>(jsonString);
                }
                else
                {
                    return false;
                }

                if (responseMessage?.Data == null) return false;

                if (invalidateGracePeriod)
                {
                    cache.Remove(key);
                }
                else
                {
                    responseMessage.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
                    if (invalidateGracePeriod)
                    {
                        responseMessage.GraceTillUtc = responseMessage.ExpiryOnUtc;
                    }

                    var options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = responseMessage.GraceTillUtc - DateTime.UtcNow
                    };
                    cache.Set(key, responseMessage, options);
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool ExpiremeAsync<T>(this IMemoryCache cache,
            object queryObject,
            bool invalidateGracePeriod = true)
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            string? key;
            if (queryObject is string) key = queryObject as string;
            else
            {
                var json = JsonSerializer.Serialize(queryObject);
                key = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");
            }

            return cache.ExpiremeAsync(key!, invalidateGracePeriod);
        }
    }
}