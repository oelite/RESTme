using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OElite.Abstractions;

namespace OElite.Restme.Hosting.Memory
{
    /// <summary>
    /// IMemoryCache implementation using OElite.Restme.MemoryCacheProvider
    /// This adapter allows OElite memory provider to integrate with ASP.NET Core IMemoryCache
    /// </summary>
    public class RestmeMemoryCache : IMemoryCache
    {
        private readonly ICacheProvider _cacheProvider;
        private readonly string? _instancePrefix;
        private bool _disposed = false;

        public RestmeMemoryCache(ICacheProvider cacheProvider, string? instancePrefix = null)
        {
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _instancePrefix = instancePrefix;
        }

        private string GetFullKey(object key)
        {
            var keyString = key?.ToString() ?? throw new ArgumentNullException(nameof(key));
            return string.IsNullOrEmpty(_instancePrefix) ? keyString : $"{_instancePrefix}{keyString}";
        }

        public bool TryGetValue(object key, out object? value)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                var cacheKey = GetFullKey(key);
                var result = _cacheProvider.GetAsync<object>(cacheKey).GetAwaiter().GetResult();

                if (result != null)
                {
                    value = result;
                    return true;
                }
            }
            catch (Exception)
            {
                // IMemoryCache.TryGetValue should not throw exceptions
                // Log the exception if needed, but don't propagate it
            }

            value = null;
            return false;
        }

        public ICacheEntry CreateEntry(object key)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return new OEliteCacheEntry(key, this);
        }

        public void Remove(object key)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                var cacheKey = GetFullKey(key);
                _cacheProvider.RemoveAsync(cacheKey).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // IMemoryCache.Remove should not throw exceptions
                // Log the exception if needed, but don't propagate it
            }
        }

        internal void Set(object key, object? value, MemoryCacheEntryOptions? options)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                Remove(key);
                return;
            }

            try
            {
                var cacheKey = GetFullKey(key);
                var expiry = GetExpiry(options);

                _cacheProvider.SetAsync(cacheKey, value, expiry).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Log the exception if needed, but don't propagate it
            }
        }

        private TimeSpan? GetExpiry(MemoryCacheEntryOptions? options)
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RestmeMemoryCache));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Note: We don't dispose the _cacheProvider here since it might be shared
                // The DI container will handle the lifecycle of the ICacheProvider
            }
        }
    }

    /// <summary>
    /// Implementation of ICacheEntry for OElite Memory Cache
    /// </summary>
    internal class OEliteCacheEntry : ICacheEntry
    {
        private readonly RestmeMemoryCache _cache;
        private MemoryCacheEntryOptions? _options;

        public OEliteCacheEntry(object key, RestmeMemoryCache cache)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public object Key { get; }

        public object? Value { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        public TimeSpan? SlidingExpiration { get; set; }

        public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();

        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();

        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

        public long? Size { get; set; }

        public void Dispose()
        {
            // Convert properties to MemoryCacheEntryOptions
            _options = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = AbsoluteExpiration,
                AbsoluteExpirationRelativeToNow = AbsoluteExpirationRelativeToNow,
                SlidingExpiration = SlidingExpiration,
                Priority = Priority,
                Size = Size
            };

            // Copy expiration tokens and callbacks
            foreach (var token in ExpirationTokens)
            {
                _options.ExpirationTokens.Add(token);
            }

            foreach (var callback in PostEvictionCallbacks)
            {
                _options.PostEvictionCallbacks.Add(callback);
            }

            // Set the value in the cache
            _cache.Set(Key, Value, _options);
        }
    }
}