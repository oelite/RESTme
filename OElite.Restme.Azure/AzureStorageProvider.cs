using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OElite.Abstractions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace OElite.Providers
{
    /// <summary>
    /// Azure Blob Storage implementation of IStorageProvider
    /// </summary>
    public class AzureStorageProvider : IStorageProvider
    {
        private readonly CloudBlobClient _blobClient;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public AzureStorageProvider(string connectionString, RestConfig config)
        {
            _config = config;
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
                if (blobItemPath.IsNullOrEmpty())
                    throw new OEliteWebException("Invalid blob item name.");

                var blockBlob = container.GetBlockBlobReference(blobItemPath);
                
                if (!await blockBlob.ExistsAsync())
                    return null;

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    using var stream = new MemoryStream();
                    await blockBlob.DownloadToStreamAsync(stream);
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

        public async Task<T?> PutAsync<T>(string key, T value) where T : class
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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
                    var jsonValue = value.JsonSerialize(_config.UseRestConvertForCollectionSerialization, _config.SerializerSettings);
                    await blockBlob.UploadTextAsync(jsonValue);
                }

                return value;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to upload blob '{key}': {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<string?> GetStringAsync(string key)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<string?> PutStringAsync(string key, string value)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<Stream?> GetStreamAsync(string key)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<bool> PutStreamAsync(string key, Stream stream)
        {
            try
            {
                var container = await GetContainerAsync(key);
                var blobItemPath = IdentifyBlobItemPath(key);
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

        public async Task<T?> GetStreamAsync<T>(string key) where T : Stream
        {
            try
            {
                var stream = await GetStreamAsync(key);
                if (stream == null)
                    return null;

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
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to get blob stream as type '{typeof(T).Name}' for '{key}': {ex.Message}", ex);
            }
        }

        private async Task<CloudBlobContainer> GetContainerAsync(string storageRelativePath)
        {
            var containerName = GetContainerName(storageRelativePath);
            var container = _blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        private string GetContainerName(string storageRelativePath)
        {
            if (storageRelativePath.IsNullOrEmpty())
                throw new OEliteWebException("Storage relative path cannot be null or empty.");

            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : "default";
        }

        private string? IdentifyBlobItemPath(string storageRelativePath)
        {
            if (storageRelativePath.IsNullOrEmpty())
                return null;

            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
                return null;

            return string.Join("/", segments, 1, segments.Length - 1);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // CloudBlobClient doesn't implement IDisposable in older versions
                // Just mark as disposed
                _disposed = true;
            }
        }
    }
}
