using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
namespace OElite
{
    public partial class Restme
    {
        private CloudStorageAccount azureStorageAccount;
        private CloudBlobClient azureBlobClient;

        public AccessCondition DefaultAzureBlobAccessCondition { get; set; }
        public BlobRequestOptions DefaultAzureBlobRequestOptions { get; set; }
        public OperationContext DefaultAzureBlobOperationContext { get; set; }


        public bool CreateAzureBlobContainerIfNotExists { get; set; }

        private void PrepareStorageRestme()
        {
            if (ConnectionString.IsNullOrEmpty())
                throw new OEliteWebException("Unable to fetch azure storage connection string.");

            azureStorageAccount = CloudStorageAccount.Parse(ConnectionString);
            azureBlobClient = azureStorageAccount.CreateCloudBlobClient();
        }
        internal async Task<CloudBlobContainer> GetAzureBlobContainerAsync(string relativePath)
        {
            var containerName = IdentifyBlobContainerName(relativePath);
            var container = azureBlobClient.GetContainerReference(containerName);
            if (CreateAzureBlobContainerIfNotExists)
                await container.CreateIfNotExistsAsync();
            return container;
        }
        internal string IdentifyBlobContainerName(string relativePath)
        {
            relativePath = relativePath?.Replace('\\', '/')?.Replace("//", "/").Trim('/');
            var indexOfFirstSegmentEnd = relativePath?.IndexOf('/');
            if (indexOfFirstSegmentEnd > 0)
                return relativePath.Substring(0, indexOfFirstSegmentEnd.Value).Trim('/');
            else
                throw new OEliteWebException("Unable to identify azure blob container name.");
        }
        internal string IdentifyBlobItemPath(string relativePath)
        {
            return relativePath?.Replace('\\', '/')?
                .Replace("//", "/")?
                .Trim('/')?
                .Replace(IdentifyBlobContainerName(relativePath), string.Empty)?
                .Trim('/');
        }
    }
}
