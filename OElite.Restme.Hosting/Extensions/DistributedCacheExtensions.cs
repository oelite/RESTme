using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace OElite.Restme.Hosting.Extensions
{
    public static class DistributedCacheExtensions
    {
        private const int DefaultCacheExpiryInSeconds = 60;

        public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
            string key,
            bool returnExpired = false,
            bool returnInGrace = true,
            Func<T, Task<bool>>? additionalValidation = null,
            Func<Task<T>>? refreshAction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (!typeof(Rest).IsAssignableFrom(typeof(T)))
                throw new InvalidOperationException("FindmeAsync only supports Rest implementation types");

            var sw = Stopwatch.StartNew();
            var cachedData = await cache.GetStringAsync(key, cancellationToken);
            sw.Stop();

            if (string.IsNullOrEmpty(cachedData))
            {
                if (refreshAction == null) return null;
                return await refreshAction();
            }

            try
            {
                var responseMessage = JsonSerializer.Deserialize<ResponseMessage>(cachedData);
                if (responseMessage?.Data == null) return null;

                var result = JsonSerializer.Deserialize<T>(responseMessage.Data.ToString()!);
                if (result == null) return null;

                var customValidationResult = additionalValidation == null || await additionalValidation.Invoke(result);
                if (!customValidationResult)
                {
                    if (refreshAction == null) return null;
                    return await refreshAction();
                }

                if (returnExpired) return result;

                if (returnInGrace && responseMessage.GraceTillUtc >= DateTime.UtcNow)
                {
                    if (responseMessage.ExpiryOnUtc <= DateTime.UtcNow && refreshAction != null)
                    {
                        refreshAction().ConfigureAwait(false);
                    }
                    return result;
                }

                if (responseMessage.ExpiryOnUtc >= DateTime.UtcNow) return result;

                return refreshAction != null ? await refreshAction() : null;
            }
            catch (JsonException)
            {
                if (refreshAction == null) return null;
                return await refreshAction();
            }
        }

        public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
            object queryObject,
            bool returnExpired = false,
            bool returnInGrace = true,
            Func<T, Task<bool>>? additionalValidation = null,
            Func<Task<T>>? refreshAction = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var json = JsonSerializer.Serialize(queryObject);
            var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

            return await cache.FindmeAsync(md5, returnExpired, returnInGrace, additionalValidation, refreshAction, cancellationToken);
        }

        public static async Task CachemeAsync<T>(this IDistributedCache cache,
            string key,
            T data,
            int expiryInSeconds = -1,
            int graceInSeconds = -1,
            CancellationToken cancellationToken = default) where T : class
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

            var serializedData = JsonSerializer.Serialize(responseMessage);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = grace - DateTime.UtcNow
            };

            await cache.SetStringAsync(key, serializedData, options, cancellationToken);
        }

        public static async Task CachemeAsync<T>(this IDistributedCache cache,
            object queryObject,
            T data,
            int expiryInSeconds = -1,
            int graceInSeconds = -1,
            CancellationToken cancellationToken = default) where T : class
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var json = JsonSerializer.Serialize(queryObject);
            var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

            await cache.CachemeAsync(md5, data, expiryInSeconds, graceInSeconds, cancellationToken);
        }

        public static async Task<bool> ExpiremeAsync(this IDistributedCache cache,
            string key,
            bool invalidateGracePeriod = true,
            CancellationToken cancellationToken = default)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var cachedData = await cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(cachedData)) return false;

            try
            {
                var responseMessage = JsonSerializer.Deserialize<ResponseMessage>(cachedData);
                if (responseMessage?.Data == null) return false;

                if (invalidateGracePeriod)
                {
                    await cache.RemoveAsync(key, cancellationToken);
                }
                else
                {
                    responseMessage.ExpiryOnUtc = DateTime.UtcNow.AddMilliseconds(-1);
                    if (invalidateGracePeriod)
                    {
                        responseMessage.GraceTillUtc = responseMessage.ExpiryOnUtc;
                    }

                    var serializedData = JsonSerializer.Serialize(responseMessage);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = responseMessage.GraceTillUtc - DateTime.UtcNow
                    };
                    await cache.SetStringAsync(key, serializedData, options, cancellationToken);
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static async Task<bool> ExpiremeAsync<T>(this IDistributedCache cache,
            object queryObject,
            bool invalidateGracePeriod = true,
            CancellationToken cancellationToken = default)
        {
            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var json = JsonSerializer.Serialize(queryObject);
            var md5 = EncryptHelper.Md5Encrypt($"{typeof(T).Name}:{json}");

            return await cache.ExpiremeAsync(md5, invalidateGracePeriod, cancellationToken);
        }
    }
}