using System;
using System.Threading.Tasks;
using OElite.Abstractions;
using StackExchange.Redis;

namespace OElite.Providers
{
    /// <summary>
    /// Redis implementation of ICacheProvider
    /// </summary>
    public class RedisCacheProvider : ICacheProvider
    {
        private readonly IDatabase _database;
        private readonly ConnectionMultiplexer _connection;
        private bool _disposed = false;

        public RedisCacheProvider(string connectionString, RestConfig config)
        {
            _connection = ConnectionMultiplexer.Connect(connectionString);
            _database = _connection.GetDatabase();
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                if (!value.HasValue)
                    return null;

                var stringValue = value.ToString();
                if (typeof(T) == typeof(string))
                    return (T)(object)stringValue;

                return stringValue.JsonDeserialize<T>();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get cache value for key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            try
            {
                string jsonValue;
                if (typeof(T) == typeof(string))
                    jsonValue = value.ToString()!;
                else
                    jsonValue = value.JsonSerialize();

                return await _database.StringSetAsync(key, jsonValue, expiry);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to set cache value for key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to remove cache key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to check cache key existence '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            try
            {
                return await _database.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to set expiry for cache key '{key}': {ex.Message}", ex);
            }
        }

        public T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
        {
            return responseMessage?.GetOriginalData<T>();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
