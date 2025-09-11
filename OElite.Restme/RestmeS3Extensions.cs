using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OElite.Data;
using System.Linq;
using Amazon.S3;
using Amazon.S3.Model;

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

        private static string? S3ObjectKey(this string storageRelativePath)
        {
            if (!storageRelativePath.IsNotNullOrEmpty()) return null;
            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1) return null;
            
            // Join all segments except the first one (bucket name) to form the object key
            return string.Join("/", segments.Skip(1));
        }

        public static async Task<T?> S3GetAsync<T>(this Rest restme, string? storageRelativePath)
        {
            MustBeS3Mode(restme);
            
            if (restme.S3Client == null)
                throw new OEliteWebException("S3 client not initialized.");
                
            var bucketName = storageRelativePath.S3BucketName();
            var objectKey = storageRelativePath.S3ObjectKey();
            
            if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                using var response = await restme.S3Client.GetObjectAsync(request);
                
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    var bytes = FileUtils.ReadStreamToEnd(response.ResponseStream);
                    T? result;
                    if (typeof(T).GetTypeInfo().IsAbstract)
                    {
                        result = (T)Activator.CreateInstance(typeof(MemoryStream), bytes)!;
                    }
                    else
                        result = (T)Activator.CreateInstance(typeof(T), bytes)!;

                    return result;
                }

                using var reader = new StreamReader(response.ResponseStream);
                var content = await reader.ReadToEndAsync();
                
                if (!content.IsNotNullOrEmpty()) return default(T);

                if (typeof(T) == typeof(string))
                    return (T)Convert.ChangeType(content, typeof(T));

                return content.JsonDeserialize<T>();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return default(T);
            }
            catch (Exception? ex)
            {
                restme.LogDebug(
                    $"Unable to fetch requested S3 object: {storageRelativePath}\n {ex.Message} \n {ex.StackTrace}", ex);
                return default(T);
            }
        }

        public static async Task<T?> S3PostAsync<T>(this Rest restme, string? storageRelativePath, object? dataObject)
        {
            MustBeS3Mode(restme);
            
            if (restme.S3Client == null)
                throw new OEliteWebException("S3 client not initialized.");
                
            if (dataObject == null)
                throw new OEliteWebException(
                    "Uploading null object is not supported, use delete method if you intended to delete.");

            var bucketName = storageRelativePath.S3BucketName();
            var objectKey = storageRelativePath.S3ObjectKey();
            
            if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                // Set content type based on file extension
                var extension = FileUtils.GetFileExtensionName(storageRelativePath);
                if (extension.IsNotNullOrEmpty())
                    request.ContentType = FileUtils.GetMimeType(extension);

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    if (dataObject is not Stream stream) return (T)dataObject;
                    stream.Position = 0;
                    request.InputStream = stream;
                }
                else
                {
                    var jsonValue = dataObject.JsonSerialize(restme.Configuration.UseRestConvertForCollectionSerialization,
                        restme.Configuration.SerializerSettings);
                    request.ContentBody = jsonValue;
                    request.ContentType = "application/json";
                }

                await restme.S3Client.PutObjectAsync(request);
                return (T)dataObject;
            }
            catch (Exception? ex)
            {
                restme.LogDebug("Unable to upload requested data to S3:\n" + ex.Message, ex);
                return default(T);
            }
        }

        public static async Task<T?> S3DeleteAsync<T>(this Rest restme, string? storageRelativePath)
        {
            MustBeS3Mode(restme);
            
            if (restme.S3Client == null)
                throw new OEliteWebException("S3 client not initialized.");
                
            var bucketName = storageRelativePath.S3BucketName();
            var objectKey = storageRelativePath.S3ObjectKey();
            
            if (bucketName.IsNullOrEmpty() || objectKey.IsNullOrEmpty())
                throw new OEliteWebException("Invalid S3 path. Expected format: bucket-name/object-key");

            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                await restme.S3Client.DeleteObjectAsync(request);
                
                if (typeof(T) == typeof(bool))
                    return (T)Convert.ChangeType(true, typeof(T));
            }
            catch (Exception? ex)
            {
                restme.LogDebug("Unable to delete requested S3 object:\n" + ex.Message, ex);
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