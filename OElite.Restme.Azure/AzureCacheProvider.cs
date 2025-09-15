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
    public class AzureCacheProvider : BaseCacheProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly CloudBlobContainer _container;
        private readonly AzureConfiguration _azureConfig;

        public AzureCacheProvider(string connectionString, RestConfig config) : base(config)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            try
            {
                _azureConfig = AzureConnectionStringParser.ParseConnectionString(connectionString);
                var storageAccount = CloudStorageAccount.Parse(_azureConfig.ConnectionString);
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

        public override async Task<T?> GetAsync<T>(string key) where T : class
        {
            ValidateKey(key, "GetAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                
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

        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            ValidateKey(key, "SetAsync");
            ValidateValue(value, "SetAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
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

        public override async Task<bool> RemoveAsync(string key)
        {
            ValidateKey(key, "RemoveAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                return await blob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove cached item with key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key)
        {
            ValidateKey(key, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                return await blob.ExistsAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to check if cached item exists with key '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                
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

        public override void Dispose()
        {
            // CloudBlobClient doesn't need explicit disposal in older versions
            // but we can clean up any resources if needed
        }
    }
}
