using System;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;
using StackExchange.Redis;

namespace OElite.Providers
{
    /// <summary>
    /// Redis implementation of ICacheProvider
    /// </summary>
    public class RedisCacheProvider : BaseCacheProvider
    {
        private readonly IDatabase _database;
        private readonly ConnectionMultiplexer _connection;

        public RedisCacheProvider(string connectionString, RestConfig config) : base(config)
        {
            _connection = ConnectionMultiplexer.Connect(connectionString);
            _database = _connection.GetDatabase();
        }

        public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            ValidateKey(key, "GetAsync");

            try
            {
                var value = await _database.StringGetAsync(key);
                cancellationToken.ThrowIfCancellationRequested();

                if (!value.HasValue)
                    return null;

                var stringValue = value.ToString();
                if (typeof(T) == typeof(string))
                    return (T)(object)stringValue;

                return stringValue.JsonDeserialize<T>();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get cache value for key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            ValidateKey(key, "SetAsync");
            ValidateValue(value, "SetAsync");

            try
            {
                string jsonValue;
                if (typeof(T) == typeof(string))
                    jsonValue = value.ToString()!;
                else
                    jsonValue = value.JsonSerialize();

                cancellationToken.ThrowIfCancellationRequested();
                return await _database.StringSetAsync(key, jsonValue, expiry);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to set cache value for key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "RemoveAsync");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to remove cache key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "ExistsAsync");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to check cache key existence '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await _database.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to set expiry for cache key '{key}': {ex.Message}", ex);
            }
        }

        public override void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
