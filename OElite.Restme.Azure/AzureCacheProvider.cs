using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using OElite.Restme;
using OElite.Restme.Abstractions;
using OElite.Restme.Azure;

namespace OElite.Providers
{
    /// <summary>
    /// Azure Blob Storage implementation of ICacheProvider
    /// Uses Azure Blob Storage as a cache layer, useful for CDN scenarios
    /// </summary>
    public class AzureCacheProvider : BaseCacheProvider
    {
        private readonly CloudBlobClient _blobClient;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public override string ProviderName => "AzureCache";

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Cache;

        /// <summary>
        /// Ensure the cache container exists
        /// </summary>
        private async Task EnsureContainerExistsAsync()
        {
            if (_container != null)
            {
                await _container.CreateIfNotExistsAsync();
            }
        }

        private readonly CloudBlobContainer _container;
        private readonly string? _rootPath;

        public AzureCacheProvider(RestConfig config) : base(config)
        {
            try
            {

                // Use pre-parsed config values directly
                var accountName = config.AuthKey;
                var accountKey = config.AuthSecret;

                // If AuthKey/AuthSecret are not provided, try parsing connection string
                if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
                {
                    if (!string.IsNullOrEmpty(config.ConnectionString))
                    {
                        var azureConfig = AzureConnectionStringParser.ParseConnectionString(config.ConnectionString);
                        var storageAccount = CloudStorageAccount.Parse(azureConfig.ConnectionString);
                        _blobClient = storageAccount.CreateCloudBlobClient();
                        _rootPath = azureConfig.RootPath;
                    }
                    else
                    {
                        throw new InvalidOperationException("Azure credentials not provided. Set AuthKey and AuthSecret in RestConfig, or provide a connection string.");
                    }
                }
                else
                {
                    // Use AuthKey/AuthSecret directly
                    var credentials = new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(accountName, accountKey);
                    var storageAccount = new CloudStorageAccount(credentials, config.Endpoint ?? "core.windows.net", useHttps: config.RestSsl);
                    _blobClient = storageAccount.CreateCloudBlobClient();
                    _rootPath = config.RootPath;
                }

                // Use a dedicated cache container
                var containerName = "restme-cache";
                _container = _blobClient.GetContainerReference(containerName);

                // Note: Container creation is now done lazily to avoid authentication during construction
                // This allows for unit testing without requiring actual Azure credentials
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Azure cache provider: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Initialize the provider asynchronously
        /// </summary>
        public async Task InitializeAsync()
        {
            // Container creation is already done in constructor
            // This method exists for interface compatibility
            await Task.CompletedTask;
        }

        /// <summary>
        /// Dispose the provider
        /// </summary>
        public async Task DisposeAsync()
        {
            // Cleanup resources if needed
            await Task.CompletedTask;
        }


        public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            ValidateKey(key, "GetAsync");

            try
            {
                // Ensure container exists before operations
                await EnsureContainerExistsAsync();
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_rootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blob.ExistsAsync())
                    return null;

                cancellationToken.ThrowIfCancellationRequested();
                var textValue = await blob.DownloadTextAsync();

                // Special handling for strings to avoid JSON serialization wrapper
                if (typeof(T) == typeof(string))
                    return (T)(object)textValue;

                return textValue.JsonDeserialize<T>();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException($"Failed to get cached item with key '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            ValidateKey(key, "SetAsync");
            ValidateValue(value, "SetAsync");

            try
            {
                // Ensure container exists before operations
                await EnsureContainerExistsAsync();
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_rootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);

                // Special handling for strings to avoid JSON serialization wrapper
                string textValue;
                if (typeof(T) == typeof(string))
                    textValue = value.ToString()!;
                else
                    textValue = value.JsonSerialize();

                // Set cache control headers for CDN scenarios
                blob.Properties.CacheControl = "public, max-age=3600"; // Default 1 hour

                if (expiry.HasValue)
                {
                    var maxAge = (int)expiry.Value.TotalSeconds;
                    blob.Properties.CacheControl = $"public, max-age={maxAge}";
                }

                cancellationToken.ThrowIfCancellationRequested();
                await blob.UploadTextAsync(textValue);

                // Set metadata for expiry tracking
                if (expiry.HasValue)
                {
                    var expiryTime = DateTime.UtcNow.Add(expiry.Value);
                    blob.Metadata["expiry"] = expiryTime.ToString("O");
                    cancellationToken.ThrowIfCancellationRequested();
                    await blob.SetMetadataAsync();
                }

                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException($"Failed to set cached item with key '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "RemoveAsync");

            try
            {
                // Ensure container exists before operations
                await EnsureContainerExistsAsync();
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_rootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                cancellationToken.ThrowIfCancellationRequested();
                return await blob.DeleteIfExistsAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException($"Failed to remove cached item with key '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_rootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);
                cancellationToken.ThrowIfCancellationRequested();
                return await blob.ExistsAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException($"Failed to check if cached item exists with key '{key}': {ex.Message}", ex);
            }
        }


        public override async Task<bool> SetExpiryAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            ValidateKey(key, "SetExpiryAsync");

            try
            {
                // Apply root path if specified
                var blobKey = AzureConnectionStringParser.CombinePath(_rootPath, key);
                var blob = _container.GetBlockBlobReference(blobKey);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blob.ExistsAsync())
                    return false;

                // Update cache control and metadata
                var maxAge = (int)expiry.TotalSeconds;
                blob.Properties.CacheControl = $"public, max-age={maxAge}";

                var expiryTime = DateTime.UtcNow.Add(expiry);
                blob.Metadata["expiry"] = expiryTime.ToString("O");

                cancellationToken.ThrowIfCancellationRequested();
                await blob.SetPropertiesAsync();
                await blob.SetMetadataAsync();

                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
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
