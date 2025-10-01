using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;
using OElite.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OElite.Providers
{
    /// <summary>
    /// Azure Blob Storage implementation of IStorageProvider
    /// </summary>
    public class AzureStorageProvider : BaseStorageProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly AzureConfiguration _azureConfig;

        public AzureStorageProvider(string connectionString, RestConfig config) : base(config)
        {
            _azureConfig = AzureConnectionStringParser.ParseConnectionString(connectionString);
            var storageAccount = CloudStorageAccount.Parse(_azureConfig.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public override async Task<T?> GetAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

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

                return jsonStringValue.JsonDeserialize<T>();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteWebException($"Failed to get blob '{objectKey}': {ex.Message}", ex);
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
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

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
                throw new OEliteWebException($"Failed to upload blob '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "DeleteAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                cancellationToken.ThrowIfCancellationRequested();
                return await blockBlob.DeleteIfExistsAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteWebException($"Failed to delete blob '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

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
                throw new OEliteWebException($"Failed to check blob existence '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> GetStringAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStringAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                cancellationToken.ThrowIfCancellationRequested();
                if (!await blockBlob.ExistsAsync())
                    return null;

                cancellationToken.ThrowIfCancellationRequested();
                return await blockBlob.DownloadTextAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteWebException($"Failed to get blob string '{objectKey}': {ex.Message}", ex);
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
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                cancellationToken.ThrowIfCancellationRequested();
                await blockBlob.UploadTextAsync(value);

                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteWebException($"Failed to upload blob string '{objectKey}': {ex.Message}", ex);
            }
        }

        public override async Task<Stream?> GetStreamAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateKey(objectKey, "GetStreamAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

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
                throw new OEliteWebException($"Failed to get blob stream '{objectKey}': {ex.Message}", ex);
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
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, objectKey);

                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

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
                throw new OEliteWebException($"Failed to upload blob stream '{objectKey}': {ex.Message}", ex);
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
                throw new OEliteWebException($"Failed to get blob stream as type '{typeof(T).Name}' for '{objectKey}': {ex.Message}", ex);
            }
        }

        private async Task<CloudBlobContainer> GetContainerAsync(string storageRelativePath)
        {
            var containerName = AzureConnectionStringParser.GetContainerName(storageRelativePath);
            var container = _blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
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
