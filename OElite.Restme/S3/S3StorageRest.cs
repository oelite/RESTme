using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OElite
{
    public partial class Rest
    {
        public AmazonS3Client? S3Client { get; private set; }

        private void PrepareS3StorageRestme()
        {
            if (ConnectionString.IsNullOrEmpty())
                throw new OEliteWebException("Unable to fetch s3 endpoint connection string.");
            
            if (Configuration?.RestKey?.IsNotNullOrEmpty() == true &&
                Configuration.RestSecret.IsNotNullOrEmpty())
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = ConnectionString,
                    ForcePathStyle = true, // Required for S3-compatible services
                    UseHttp = !Configuration.RestSsl
                };

                S3Client = new AmazonS3Client(Configuration.RestKey, Configuration.RestSecret, config);

                Initialized = true;
            }
            else
            {
                throw new OEliteWebException("Invalid S3 Access Key & Secret");
            }
        }
    }
}