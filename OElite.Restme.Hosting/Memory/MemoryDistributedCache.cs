using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Hosting.Memory
{
    /// <summary>
    /// IDistributedCache implementation using OElite.Restme.MemoryCacheProvider
    /// This adapter allows OElite memory provider to integrate with ASP.NET Core IDistributedCache
    /// </summary>
    public class MemoryDistributedCache : IDistributedCache
    {
        private readonly ICacheProvider _cacheProvider;
        private readonly string? _instancePrefix;

        public MemoryDistributedCache(ICacheProvider cacheProvider, string? instancePrefix = null)
        {
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _instancePrefix = instancePrefix;
        }

        private string GetFullKey(string key)
        {
            return string.IsNullOrEmpty(_instancePrefix) ? key : $"{_instancePrefix}{key}";
        }

        public byte[]? Get(string key)
        {
            return GetAsync(key).GetAwaiter().GetResult();
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                var value = await _cacheProvider.GetAsync<string>(GetFullKey(key), token);
                if (value == null)
                    return null;

                return Encoding.UTF8.GetBytes(value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get cache value for key '{key}'", ex);
            }
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options).GetAwaiter().GetResult();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var stringValue = Encoding.UTF8.GetString(value);
                var expiry = GetExpiry(options);

                await _cacheProvider.SetAsync(GetFullKey(key), stringValue, expiry, token);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set cache value for key '{key}'", ex);
            }
        }

        public void Refresh(string key)
        {
            RefreshAsync(key).GetAwaiter().GetResult();
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            // IDistributedCache Refresh is meant to reset sliding expiration
            // For now, we just check if the key exists to keep it alive
            // A proper implementation would require extending ICacheProvider with sliding expiration support
            await _cacheProvider.ExistsAsync(GetFullKey(key), token);
        }

        public void Remove(string key)
        {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                await _cacheProvider.RemoveAsync(GetFullKey(key), token);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove cache key '{key}'", ex);
            }
        }

        private TimeSpan? GetExpiry(DistributedCacheEntryOptions? options)
        {
            if (options == null)
                return null;

            // Prefer AbsoluteExpiration over AbsoluteExpirationRelativeToNow
            if (options.AbsoluteExpiration.HasValue)
            {
                var timeUntilExpiry = options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
                return timeUntilExpiry > TimeSpan.Zero ? timeUntilExpiry : TimeSpan.Zero;
            }

            if (options.AbsoluteExpirationRelativeToNow.HasValue)
                return options.AbsoluteExpirationRelativeToNow.Value;

            // SlidingExpiration is not fully supported yet - would require extending ICacheProvider
            if (options.SlidingExpiration.HasValue)
                return options.SlidingExpiration.Value;

            return null;
        }
    }
}