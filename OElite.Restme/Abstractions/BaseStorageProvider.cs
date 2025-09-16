using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Base implementation for storage providers with common functionality
    /// </summary>
    public abstract class BaseStorageProvider(RestConfig config) : IStorageProvider
    {
        protected readonly RestConfig Config = config ?? throw new ArgumentNullException(nameof(config));
        protected bool Disposed = false;

        public abstract Task<T?> GetAsync<T>(string objectKey) where T : class;
        public abstract Task<T?> PutAsync<T>(string objectKey, T value) where T : class;
        public abstract Task<bool> DeleteAsync(string objectKey);
        public abstract Task<bool> ExistsAsync(string objectKey);
        public abstract Task<string?> GetStringAsync(string objectKey);
        public abstract Task<string?> PutStringAsync(string objectKey, string value);
        public abstract Task<Stream?> GetStreamAsync(string objectKey);
        public abstract Task<bool> PutStreamAsync(string objectKey, Stream stream);
        public abstract Task<T> GetStreamAsync<T>(string objectKey) where T : Stream;
        public abstract void Dispose();

        /// <summary>
        /// Common implementation for handling Stream types in GetAsync
        /// </summary>
        protected T? HandleStreamType<T>(Stream responseStream) where T : class
        {
            if (!typeof(Stream).IsAssignableFrom(typeof(T)))
                return null;

            var bytes = FileUtils.ReadStreamToEnd(responseStream);

            T? result;
            if (typeof(T).GetTypeInfo().IsAbstract)
            {
                result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
            }
            else
                result = (T)Activator.CreateInstance(typeof(T), bytes)!;

            return result;
        }

        /// <summary>
        /// Common implementation for handling Stream types in GetStreamAsync
        /// </summary>
        protected T HandleStreamTypeForStream<T>(Stream? stream)
        {
            if (stream == null || typeof(Stream).IsAssignableFrom(typeof(T)))
                return default;

            var bytes = FileUtils.ReadStreamToEnd(stream);

            T? result;
            if (typeof(T).GetTypeInfo().IsAbstract)
            {
                result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
            }
            else
                result = (T)Activator.CreateInstance(typeof(T), bytes)!;

            return result;
        }

        /// <summary>
        /// Common implementation for handling Stream types in PutAsync
        /// </summary>
        protected async Task<bool> HandleStreamPutAsync<T>(T value, Func<Stream, Task> uploadAction) where T : class
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                if (value is not Stream stream)
                    return false;

                stream.Position = 0;
                await uploadAction(stream);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates storage key input
        /// </summary>
        protected static void ValidateKey(string key, string operation)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException($"Storage key cannot be null or empty for {operation}", nameof(key));
        }

        /// <summary>
        /// Validates storage value input
        /// </summary>
        protected static void ValidateValue<T>(T value, string operation) where T : class
        {
            if (value == null)
                throw new ArgumentException($"Storage value cannot be null for {operation}", nameof(value));
        }

        /// <summary>
        /// Validates that the provider is not disposed
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (Disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}