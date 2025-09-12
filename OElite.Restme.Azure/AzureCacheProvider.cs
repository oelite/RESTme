using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using OElite.Abstractions;
using OElite.Utils;

namespace OElite.Providers
{
    /// <summary>
    /// Azure Blob Storage implementation of ICacheProvider
    /// Uses Azure Blob Storage as a cache layer, useful for CDN scenarios
    /// </summary>
    public class AzureCacheProvider : ICacheProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly CloudBlobContainer _container;
        private readonly RestConfig _config;

        public AzureCacheProvider(string connectionString, RestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            try
            {
                var storageAccount = CloudStorageAccount.Parse(connectionString);
                _blobClient = storageAccount.CreateCloudBlobClient();
                
                // Use a dedicated cache container
                var containerName = "restme-cache";
                _container = _blobClient.GetContainerReference(containerName);
                
                // Create container if it doesn't exist
                _container.CreateIfNotExistsAsync().Wait();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Azure cache provider: {ex.Message}", ex);
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return null;

            try
            {
                var blob = _container.GetBlockBlobReference(key);
                
                if (!await blob.ExistsAsync())
                    return null;

                var json = await blob.DownloadTextAsync();
                return json.JsonDeserialize<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (value == null)
                return false;

            try
            {
                var blob = _container.GetBlockBlobReference(key);
                var json = value.JsonSerialize();
                
                // Set cache control headers for CDN scenarios
                blob.Properties.CacheControl = "public, max-age=3600"; // Default 1 hour
                
                if (expiry.HasValue)
                {
                    var maxAge = (int)expiry.Value.TotalSeconds;
                    blob.Properties.CacheControl = $"public, max-age={maxAge}";
                }

                await blob.UploadTextAsync(json);
                
                // Set metadata for expiry tracking
                if (expiry.HasValue)
                {
                    var expiryTime = DateTime.UtcNow.Add(expiry.Value);
                    blob.Metadata["expiry"] = expiryTime.ToString("O");
                    await blob.SetMetadataAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var blob = _container.GetBlockBlobReference(key);
                return await blob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var blob = _container.GetBlockBlobReference(key);
                return await blob.ExistsAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to check if cached item exists with key '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            try
            {
                var blob = _container.GetBlockBlobReference(key);
                
                if (!await blob.ExistsAsync())
                    return false;

                // Update cache control and metadata
                var maxAge = (int)expiry.TotalSeconds;
                blob.Properties.CacheControl = $"public, max-age={maxAge}";
                
                var expiryTime = DateTime.UtcNow.Add(expiry);
                blob.Metadata["expiry"] = expiryTime.ToString("O");
                
                await blob.SetPropertiesAsync();
                await blob.SetMetadataAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set expiry for cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
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

        public void Dispose()
        {
            // CloudBlobClient doesn't need explicit disposal in older versions
            // but we can clean up any resources if needed
        }
    }
}
