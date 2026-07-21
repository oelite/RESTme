using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Base
{
    /// <summary>
    /// In-memory cache provider for fast temporary value access
    /// No external dependencies required
    /// </summary>
    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly ConcurrentDictionary<string, (object Data, DateTime ExpiryOnUtc)> _cache;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "MemoryCache";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => new(RestMode.Memory);

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.Cache;

        public MemoryCacheProvider()
        {
            _cache = new ConcurrentDictionary<string, (object Data, DateTime ExpiryOnUtc)>();
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return Task.FromResult<T?>(null);

            if (_cache.TryGetValue(key, out var cacheEntry))
            {
                // Check expiry
                if (DateTime.UtcNow > cacheEntry.ExpiryOnUtc)
                {
                    _cache.TryRemove(key, out _);
                    return Task.FromResult<T?>(null);
                }

                return Task.FromResult(cacheEntry.Data as T);
            }

            return Task.FromResult<T?>(null);
        }

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(key) || value == null)
                return Task.FromResult(false);

            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue;
            var cacheEntry = (Data: (object)value, ExpiryOnUtc: expiryTime);

            _cache.AddOrUpdate(key, cacheEntry, (k, v) => cacheEntry);
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

            if (_cache.TryGetValue(key, out var cacheEntry))
            {
                if (DateTime.UtcNow > cacheEntry.ExpiryOnUtc)
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

            if (_cache.TryGetValue(key, out var currentEntry))
            {
                var updatedEntry = (Data: currentEntry.Data, ExpiryOnUtc: DateTime.UtcNow.Add(expiry));
                _cache.TryUpdate(key, updatedEntry, currentEntry);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Get all keys matching a glob-style pattern (Redis SCAN compatible).
        /// Supports * (any sequence) and ? (single character) wildcards.
        /// </summary>
        public IEnumerable<string> GetKeys(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return Array.Empty<string>();

            // Convert glob pattern to regex: * → .*, ? → .
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new Regex(regexPattern, RegexOptions.Compiled);

            // Filter out expired keys so callers don't operate on stale entries
            var now = DateTime.UtcNow;
            return _cache
                .Where(kvp => now <= kvp.Value.ExpiryOnUtc && regex.IsMatch(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        private void CleanupExpiredItems(object? state)
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (DateTime.UtcNow > kvp.Value.ExpiryOnUtc)
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

    }
}