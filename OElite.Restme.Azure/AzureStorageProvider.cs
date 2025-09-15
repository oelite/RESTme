using System;
using System.IO;
using System.Reflection;
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

        public override async Task<T> GetAsync<T>(string key) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                
                if (!await blockBlob.ExistsAsync())
                    return null;

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    using var stream = new MemoryStream();
                    await blockBlob.DownloadToStreamAsync(stream);
                    return HandleStreamType<T>(stream);
                }

                var jsonStringValue = await blockBlob.DownloadTextAsync();
                if (!jsonStringValue.IsNotNullOrEmpty())
                    return null;

                if (typeof(T) == typeof(string))
                    return (T)Convert.ChangeType(jsonStringValue, typeof(T));

                return jsonStringValue.JsonDeserialize<T>();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get blob '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<T> PutAsync<T>(string key, T value) where T : class
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutAsync");
            ValidateValue(value, "PutAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);

                var extension = FileUtils.GetFileExtensionName(key);
                if (extension.IsNotNullOrEmpty())
                    blockBlob.Properties.ContentType = FileUtils.GetMimeType(extension);

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    if (value is not Stream stream)
                        return value;
                    
                    stream.Position = 0;
                    await blockBlob.UploadFromStreamAsync(stream);
                }
                else
                {
                    var jsonValue = value.JsonSerialize(Config.UseRestConvertForCollectionSerialization, Config.SerializerSettings);
                    await blockBlob.UploadTextAsync(jsonValue);
                }

                return value;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload blob '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> DeleteAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "DeleteAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                return await blockBlob.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to delete blob '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> ExistsAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "ExistsAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    return false;

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                return await blockBlob.ExistsAsync();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to check blob existence '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> GetStringAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStringAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                
                if (!await blockBlob.ExistsAsync())
                    return null;

                return await blockBlob.DownloadTextAsync();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get blob string '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<string?> PutStringAsync(string key, string value)
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutStringAsync");
            ValidateValue(value, "PutStringAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                await blockBlob.UploadTextAsync(value);
                
                return value;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload blob string '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<Stream?> GetStreamAsync(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStreamAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                
                if (!await blockBlob.ExistsAsync())
                    return null;

                var stream = new MemoryStream();
                await blockBlob.DownloadToStreamAsync(stream);
                stream.Position = 0;
                
                return stream;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get blob stream '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<bool> PutStreamAsync(string key, Stream stream)
        {
            ThrowIfDisposed();
            ValidateKey(key, "PutStreamAsync");
            ValidateValue(stream, "PutStreamAsync");

            try
            {
                // Apply root path if specified
                var finalKey = AzureConnectionStringParser.CombinePath(_azureConfig.RootPath, key);
                
                var container = await GetContainerAsync(finalKey);
                var blobItemPath = AzureConnectionStringParser.GetBlobItemPath(finalKey);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                
                var extension = FileUtils.GetFileExtensionName(key);
                if (extension.IsNotNullOrEmpty())
                    blockBlob.Properties.ContentType = FileUtils.GetMimeType(extension);

                stream.Position = 0;
                await blockBlob.UploadFromStreamAsync(stream);
                
                return true;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload blob stream '{key}': {ex.Message}", ex);
            }
        }

        public override async Task<T> GetStreamAsync<T>(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key, "GetStreamAsync");

            try
            {
                var stream = await GetStreamAsync(key);
                return HandleStreamTypeForStream<T>(stream);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get blob stream as type '{typeof(T).Name}' for '{key}': {ex.Message}", ex);
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
