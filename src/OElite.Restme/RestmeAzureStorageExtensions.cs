using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeAzureStorageExtensions
    {
        public static async Task<T> StorageGetAsync<T>(this Restme restme, string storageRelativePath)
        {
            MustBeStorageMode(restme);
            var container = await restme.GetAzureBlobContainerAsync(storageRelativePath);
            var blobItemPath = restme.IdentifyBlobItemPath(storageRelativePath);
            if (blobItemPath.IsNullOrEmpty())
                throw new OEliteWebException("Invalid blob item name.");
            var blockBlob = container.GetBlockBlobReference(blobItemPath);
            using (MemoryStream stream = new MemoryStream())
            {

                try
                {
                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        await blockBlob.DownloadToStreamAsync(stream);
                        MemoryStream streamForOutput = new MemoryStream(stream.ToArray());
                        return (T)Convert.ChangeType(streamForOutput, typeof(T));
                    }
                    else
                    {
                        var jsonStringValue = await blockBlob.DownloadTextAsync();
                        if (typeof(T) == typeof(string))
                            return (T)Convert.ChangeType(jsonStringValue, typeof(T));
                        else
                            return jsonStringValue.JsonDeserialize<T>();
                    }

                }
                catch (Exception ex)
                {
                    Restme.LogDebug("Unable to fetch requested blob:" + ex.Message, ex);
                    return default(T);
                }
            }
        }

        public static async Task<T> StoragePostAsync<T>(this Restme restme, string storageRelativePath, T dataObject)
        {
            MustBeStorageMode(restme);
            if (dataObject == null)
                throw new OEliteWebException("Uploading null blob is not supported, use delete method if you intended to delete.");
            var container = await restme.GetAzureBlobContainerAsync(storageRelativePath);
            var blobItemPath = restme.IdentifyBlobItemPath(storageRelativePath);
            if (blobItemPath.IsNullOrEmpty())
                throw new OEliteWebException("Invalid blob item name.");
            var blockBlob = container.GetBlockBlobReference(blobItemPath);
            Stream stream = null;
            using (stream = new MemoryStream())
            {
                try
                {
                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        stream = dataObject as Stream;
                        stream.Position = 0;
                        await blockBlob.UploadFromStreamAsync(stream);
                    }
                    else
                    {
                        var jsonValue = dataObject.JsonSerialize();
                        await blockBlob.UploadTextAsync(jsonValue, restme._currentEncoding, restme.DefaultAzureBlobAccessCondition, restme.DefaultAzureBlobRequestOptions, restme.DefaultAzureBlobOperationContext);
                    }
                    return dataObject;
                }
                catch (Exception ex)
                {
                    Restme.LogDebug("Unable to upload requested data:\n" + ex.Message, ex);
                    return default(T);
                }
            }
        }
        public static async Task<T> StorageDeleteAsync<T>(this Restme restme, string storageRelativePath)
        {
            MustBeStorageMode(restme);
            var container = await restme.GetAzureBlobContainerAsync(storageRelativePath);
            var blobItemPath = restme.IdentifyBlobItemPath(storageRelativePath);
            if (blobItemPath.IsNullOrEmpty())
                throw new OEliteWebException("Invalid blob item name.");
            var blockBlob = container.GetBlockBlobReference(blobItemPath);
            try
            {
                await blockBlob.DeleteIfExistsAsync();
                if (typeof(T) == typeof(bool))
                    return (T)Convert.ChangeType(true, typeof(T));
            }
            catch (Exception ex)
            {
                Restme.LogDebug("Unable to delete requested data:\n" + ex.Message, ex);
            }
            return default(T);
        }

        #region Private Methods
        private static void MustBeStorageMode(Restme restme)
        {
            if (restme?.CurrentMode != RestMode.AzureStorageClient)
                throw new InvalidOperationException($"current request is not valid operation, you are under RestMode: {restme.CurrentMode.ToString()}");
        }
        #endregion
    }
}
