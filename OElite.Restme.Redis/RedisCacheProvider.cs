using System;
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

        public override async Task<T?> GetAsync<T>(string key) where T : class
        {
            ValidateKey(key, "GetAsync");

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

        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
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

                return await _database.StringSetAsync(key, jsonValue, expiry);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to set cache value for key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> RemoveAsync(string key)
        {
            ValidateKey(key, "RemoveAsync");

            try
            {
                return await _database.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to remove cache key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key)
        {
            ValidateKey(key, "ExistsAsync");

            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to check cache key existence '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                return await _database.KeyExpireAsync(key, expiry);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to set expiry for cache key '{key}': {ex.Message}", ex);
            }
        }

        public override void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
