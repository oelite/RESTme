using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite.Base
{
    /// <summary>
    /// In-memory cache provider for fast temporary value access
    /// No external dependencies required
    /// </summary>
    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public MemoryCacheProvider()
        {
            _cache = new ConcurrentDictionary<string, CacheItem>();
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return Task.FromResult<T?>(null);

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (cacheItem.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return Task.FromResult<T?>(null);
                }

                if (cacheItem.Value is T typedValue)
                    return Task.FromResult<T?>(typedValue);

                if (cacheItem.Value is ResponseMessage responseMessage)
                    return Task.FromResult(GetOriginalData<T>(responseMessage));
            }

            return Task.FromResult<T?>(null);
        }

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return Task.FromResult(false);

            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue;
            var cacheItem = new CacheItem(value, expiryTime);

            _cache.AddOrUpdate(key, cacheItem, (k, v) => cacheItem);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                return Task.FromResult(false);

            return Task.FromResult(_cache.TryRemove(key, out _));
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                return Task.FromResult(false);

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (cacheItem.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return Task.FromResult(false);
                }
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                return Task.FromResult(false);

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                var newExpiryTime = DateTime.UtcNow.Add(expiry);
                var updatedItem = new CacheItem(cacheItem.Value, newExpiryTime);
                _cache.TryUpdate(key, updatedItem, cacheItem);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
        {
            if (responseMessage?.Data is T directData)
                return directData;

            return responseMessage?.Data as T;
        }

        private void CleanupExpiredItems(object? state)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _cache.Clear();
                _disposed = true;
            }
        }

        private class CacheItem
        {
            public object Value { get; }
            public DateTime ExpiryTime { get; }

            public bool IsExpired => DateTime.UtcNow > ExpiryTime;

            public CacheItem(object value, DateTime expiryTime)
            {
                Value = value;
                ExpiryTime = expiryTime;
            }
        }
    }
}