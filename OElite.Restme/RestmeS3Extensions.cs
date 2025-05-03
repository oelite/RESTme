using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OElite.Data;
using System.Linq;

namespace OElite
{
    public static class RestmeS3Extensions
    {
        private static IEnumerable<string> IdentifiedS3ProviderEndpoints
        {
            get
            {
                var identifiedList = new List<string> { "stackpathstorage.com" }
                    .AddAmazonEndpoints();
                return identifiedList;
            }
        }

        public static bool IsS3Provider(this string connectionString)
        {
            if (connectionString.IsNotNullOrEmpty())
            {
                return IdentifiedS3ProviderEndpoints.Count(item =>
                    connectionString.ToLower().Contains(item)) > 0;
            }

            return false;
        }

        private static string? S3BucketName(this string storageRelativePath)
        {
            if (!storageRelativePath.IsNotNullOrEmpty()) return null;
            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : null;
        }

        private static string? S3FileName(this string storageRelativePath)
        {
            if (!storageRelativePath.IsNotNullOrEmpty()) return null;
            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 1 ? segments[^1] : null;
        }

        internal static string? S3ObjectPath(this string storageRelativePath, bool fromFilePath = true)
        {
            if (!storageRelativePath.IsNotNullOrEmpty()) return null;
            var result = storageRelativePath.Trim('/')
                .Replace(storageRelativePath.S3BucketName()!, string.Empty)
                .Trim('/');
            if (fromFilePath)
            {
                result = result.Replace(storageRelativePath.S3FileName()!, string.Empty)
                    .Trim('/');
            }

            return result;
        }


        public static async Task<T?> S3GetAsync<T>(this Rest restme, string? storageRelativePath)
        {
            // restme.S3Client.GetObjectAsync(storageRelativePath.S3BucketName(),storageRelativePath.S3ObjectPath())

            var container = await restme.GetAzureBlobContainerAsync(storageRelativePath);
            var blobItemPath = restme.IdentifyBlobItemPath(storageRelativePath);
            if (blobItemPath.IsNullOrEmpty())
                throw new OEliteWebException("Invalid blob item name.");
            var blockBlob = container.GetBlockBlobReference(blobItemPath);
            using var stream = new MemoryStream();
            try
            {
                if (!await blockBlob.ExistsAsync()) return default(T);

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
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
                if (!jsonStringValue.IsNotNullOrEmpty()) return default(T);

                if (typeof(T) == typeof(string))
                    return (T)Convert.ChangeType(jsonStringValue, typeof(T));

                return jsonStringValue.JsonDeserialize<T>();
            }
            catch (Exception? ex)
            {
                restme.LogDebug(
                    $"Unable to fetch requested blob: {storageRelativePath}\n {ex.Message} \n {ex.StackTrace}", ex);
                return default(T);
            }
        }

        public static async Task<T?> S3PostAsync<T>(this Rest restme, string? storageRelativePath, object? dataObject)
        {
            MustBeS3Mode(restme);
            if (dataObject == null)
                throw new OEliteWebException(
                    "Uploading null blob is not supported, use delete method if you intended to delete.");

            var container = await restme.GetAzureBlobContainerAsync(storageRelativePath);
            var blobItemPath = restme.IdentifyBlobItemPath(storageRelativePath);
            if (blobItemPath.IsNullOrEmpty())
                throw new OEliteWebException("Invalid blob item name.");
            var blockBlob = container.GetBlockBlobReference(blobItemPath);
            try
            {
                var extension = FileUtils.GetFileExtensionName(storageRelativePath);
                if (extension.IsNotNullOrEmpty())
                    blockBlob.Properties.ContentType = FileUtils.GetMimeType(extension);
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    if (dataObject is not Stream stream) return (T)dataObject;
                    stream.Position = 0;
                    await blockBlob.UploadFromStreamAsync(stream);
                }
                else
                {
                    var jsonValue =
                        dataObject.JsonSerialize(restme.Configuration.UseRestConvertForCollectionSerialization,
                            restme.Configuration.SerializerSettings);
                    await
                        blockBlob.UploadTextAsync(jsonValue, restme.Configuration.DefaultEncoding,
                            restme.DefaultAzureBlobAccessCondition, restme.DefaultAzureBlobRequestOptions,
                            restme.DefaultAzureBlobOperationContext);
                }

                return (T)dataObject;
            }
            catch (Exception? ex)
            {
                restme.LogDebug("Unable to upload requested data:\n" + ex.Message, ex);
                return default(T);
            }
        }

        public static async Task<T?> S3DeleteAsync<T>(this Rest restme, string? storageRelativePath)
        {
            MustBeS3Mode(restme);
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
            catch (Exception? ex)
            {
                restme.LogDebug("Unable to delete requested data:\n" + ex.Message, ex);
            }

            return default;
        }

        #region Private Methods

        private static void MustBeS3Mode(Rest restme)
        {
            if (restme.CurrentMode != RestMode.S3Client)
                throw new InvalidOperationException(
                    $"current request is not valid operation, you are under RestMode: {restme.CurrentMode.ToString()}");
        }

        #endregion
    }
}