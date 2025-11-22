using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OElite;
using OElite.Restme;
using OElite.Restme.Abstractions;
using StackExchange.Redis;

namespace OElite.Providers
{
    /// <summary>
    /// Redis implementation of ICacheProvider
    /// </summary>
    public class RedisCacheProvider : BaseCacheProvider
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public override string ProviderName => "RedisCache";

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Cache;
        private readonly ConnectionMultiplexer _connection;

        public RedisCacheProvider(RestConfig config) : base(config)
        {

            // Use pre-parsed config values directly
            var password = config.AuthSecret;

            // Use connection string from config
            string connectionString = config.ConnectionString ?? "localhost:6379";

            if (!string.IsNullOrEmpty(password))
            {
                // If password is provided via config, modify connection string to include it
                var connectionOptions = ConfigurationOptions.Parse(connectionString);
                connectionOptions.Password = password;
                connectionOptions.AbortOnConnectFail = false; // Allow retrying for better error handling
                _connection = ConnectionMultiplexer.Connect(connectionOptions);
            }
            else
            {
                // Use connection string as-is (may contain password) with graceful failure handling
                var connectionOptions = ConfigurationOptions.Parse(connectionString);
                connectionOptions.AbortOnConnectFail = false; // Allow retrying for better error handling
                _connection = ConnectionMultiplexer.Connect(connectionOptions);
            }

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

                var storedValue = value.ToString();

                // Redis handles expiry natively via TTL, so if we get a value, it's valid
                // Handle string type directly, otherwise deserialize from JSON
                if (typeof(T) == typeof(string))
                {
                    // Handle empty string marker - convert back to empty string
                    var resultValue = storedValue == "\u0000" ? "" : storedValue;
                    return resultValue as T;
                }

                // For complex objects, use dictionary-friendly deserialization
                return StringUtils.JsonDeserialize<T>(storedValue, CreateDictionaryFriendlySettings()) ?? default(T);
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
                // Store user data directly without tampering
                string stringValue;
                if (value is string str)
                {
                    // Handle empty string edge case for Redis compatibility
                    stringValue = str == "" ? "\u0000" : str; // Use null character as marker for empty string
                }
                else
                {
                    // For complex objects, use dictionary-friendly serialization
                    stringValue = StringUtils.JsonSerialize(value, CreateDictionaryFriendlySettings());
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Handle Redis TTL restrictions - zero and negative values are not allowed
                TimeSpan? redisExpiry = expiry;
                if (expiry.HasValue && expiry.Value <= TimeSpan.Zero)
                {
                    // Zero or negative TTL - don't set any expiry (let it persist indefinitely)
                    redisExpiry = null;
                }

                // Use Redis native TTL for expiry management
                return await _database.StringSetAsync(key, stringValue, redisExpiry);
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

        /// <summary>
        /// Creates JSON serializer settings that preserve dictionary keys but still handle object properties correctly
        /// </summary>
        private static JsonSerializerSettings CreateDictionaryFriendlySettings()
        {
            return new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                // Use default naming strategy instead of OEliteJsonResolver to preserve dictionary keys
                ContractResolver = new DefaultContractResolver()
            };
        }

        public override void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
