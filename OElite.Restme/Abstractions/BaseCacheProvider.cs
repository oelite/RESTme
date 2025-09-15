using System;
using System.Threading.Tasks;
using OElite.Utils;

namespace OElite.Abstractions
{
    /// <summary>
    /// Base implementation for cache providers with common functionality
    /// </summary>
    public abstract class BaseCacheProvider : ICacheProvider
    {
        protected readonly RestConfig Config;

        protected BaseCacheProvider(RestConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public abstract Task<T?> GetAsync<T>(string key) where T : class;
        public abstract Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
        public abstract Task<bool> RemoveAsync(string key);
        public abstract Task<bool> ExistsAsync(string key);
        public abstract Task<bool> SetExpiryAsync(string key, TimeSpan expiry);
        public abstract void Dispose();

        /// <summary>
        /// Common implementation for GetOriginalData method
        /// </summary>
        public virtual T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
        {
            if (responseMessage?.Data == null)
                return null;

            try
            {
                if (responseMessage.Data is T directData)
                    return directData;

                if (responseMessage.Data is string jsonString)
                    return jsonString.JsonDeserialize<T>();

                return responseMessage.Data.JsonSerialize().JsonDeserialize<T>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validates cache key input
        /// </summary>
        protected static void ValidateKey(string key, string operation)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException($"Cache key cannot be null or empty for {operation}", nameof(key));
        }

        /// <summary>
        /// Validates cache value input
        /// </summary>
        protected static void ValidateValue<T>(T value, string operation) where T : class
        {
            if (value == null)
                throw new ArgumentException($"Cache value cannot be null for {operation}", nameof(value));
        }
    }
}
