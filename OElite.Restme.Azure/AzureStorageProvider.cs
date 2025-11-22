using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using OElite;
using OElite.Restme;
using OElite.Restme.Abstractions;
using OElite.Restme.Azure;

namespace OElite.Providers
{
    /// <summary>
    /// Azure Blob Storage implementation of IStorageProvider
    /// </summary>
    public class AzureStorageProvider : BaseStorageProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly string? _rootPath;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public override string ProviderName => "AzureStorage";

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Storage;

        public AzureStorageProvider(RestConfig config) : base(config)
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
        }

        public override async Task<T?> GetAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blockBlob.ExistsAsync())
                    return null;

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    using var stream = new MemoryStream();
                    cancellationToken.ThrowIfCancellationRequested();
                    await blockBlob.DownloadToStreamAsync(stream);
                    return HandleStreamType<T>(stream);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var jsonStringValue = await blockBlob.DownloadTextAsync();
                if (!jsonStringValue.IsNotNullOrEmpty())
                    return null;

                if (typeof(T) == typeof(string))
                    return (T)Convert.ChangeType(jsonStringValue, typeof(T));

                return StringUtils.JsonDeserialize<T>(jsonStringValue);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get blob '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<T?> PutAsync<T>(string objectKey, T value, CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutAsync");
            ValidateValue(value, "PutAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                var extension = FileUtils.GetFileExtensionName(objectKey);
                if (extension.IsNotNullOrEmpty())
                    blockBlob.Properties.ContentType = FileUtils.GetMimeType(extension);

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    if (value is not Stream stream)
                        return value;

                    stream.Position = 0;
                    cancellationToken.ThrowIfCancellationRequested();
                    await blockBlob.UploadFromStreamAsync(stream);
                }
                else
                {
                    var jsonValue = value.JsonSerialize(Config.UseRestConvertForCollectionSerialization, Config.SerializerSettings);
                    cancellationToken.ThrowIfCancellationRequested();
                    await blockBlob.UploadTextAsync(jsonValue);
                }

                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to upload blob '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "DeleteAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                cancellationToken.ThrowIfCancellationRequested();
                return await blockBlob.DeleteIfExistsAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to delete blob '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    return false;

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                cancellationToken.ThrowIfCancellationRequested();
                return await blockBlob.ExistsAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to check blob existence '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> GetStringAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStringAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blockBlob.ExistsAsync())
                    return null;

                cancellationToken.ThrowIfCancellationRequested();
                return await blockBlob.DownloadTextAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get blob string '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> PutStringAsync(string objectKey, string value, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutStringAsync");
            ValidateValue(value, "PutStringAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                cancellationToken.ThrowIfCancellationRequested();
                await blockBlob.UploadTextAsync(value);

                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to upload blob string '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<Stream?> GetStreamAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStreamAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blockBlob.ExistsAsync())
                    return null;

                var stream = new MemoryStream();
                cancellationToken.ThrowIfCancellationRequested();
                await blockBlob.DownloadToStreamAsync(stream);
                stream.Position = 0;

                return stream;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get blob stream '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> PutStreamAsync(string objectKey, Stream stream, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "PutStreamAsync");
            ValidateValue(stream, "PutStreamAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_rootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                var extension = FileUtils.GetFileExtensionName(objectKey);
                if (extension.IsNotNullOrEmpty())
                    blockBlob.Properties.ContentType = FileUtils.GetMimeType(extension);

                stream.Position = 0;
                cancellationToken.ThrowIfCancellationRequested();
                await blockBlob.UploadFromStreamAsync(stream);

                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to upload blob stream '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<T> GetStreamAsync<T>(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStreamAsync");

            try
            {
                var stream = await GetStreamAsync(objectKey, cancellationToken);
                return HandleStreamTypeForStream<T>(stream);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to get blob stream as type '{typeof(T).Name}' for '{objectKey}': {ex.Message}", ex);
            }
        }

        private async Task<CloudBlobContainer> GetContainerAsync(string storageRelativePath)
        {
            var containerName = AzureConnectionStringParser.GetContainerName(storageRelativePath);
            var container = _blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
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

        public override void Dispose()
        {
            if (!Disposed)
            {
                // CloudBlobClient doesn't implement IDisposable in older versions
                // Just mark as disposed
                Disposed = true;
            }
        }
    }
}
