using System;
using Amazon;

namespace OElite.Utils
{
    /// <summary>
    /// S3 configuration model for parsing connection strings
    /// </summary>
    public class S3Configuration
    {
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public RegionEndpoint? Region { get; set; }
        public string? BucketName { get; set; }
        public string? ServiceUrl { get; set; }
        public bool ForcePathStyle { get; set; } = false;
        public bool UseHttp { get; set; } = false;
        public string? RootPath { get; set; }
    }

    /// <summary>
    /// Utility class for parsing S3 connection strings
    /// </summary>
    public static class S3ConnectionStringParser
    {
        /// <summary>
        /// Parses S3 connection string into configuration object
        /// </summary>
        public static S3Configuration ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            var config = new S3Configuration();
            var parts = connectionString.Split(';');
            
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;
                
                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();
                
                switch (key)
                {
                    case "accesskeyid":
                        config.AccessKeyId = value;
                        break;
                    case "secretaccesskey":
                        config.SecretAccessKey = value;
                        break;
                    case "region":
                        config.Region = RegionEndpoint.GetBySystemName(value);
                        break;
                    case "bucketname":
                        config.BucketName = value;
                        break;
                    case "serviceurl":
                    case "endpoint":
                        config.ServiceUrl = value;
                        break;
                    case "forcepathstyle":
                        config.ForcePathStyle = bool.Parse(value);
                        break;
                    case "usehttp":
                        config.UseHttp = bool.Parse(value);
                        break;
                    case "rootpath":
                        config.RootPath = value;
                        break;
                }
            }
            
            if (string.IsNullOrEmpty(config.AccessKeyId) || string.IsNullOrEmpty(config.SecretAccessKey))
                throw new ArgumentException("AccessKeyId and SecretAccessKey are required in connection string");
            
            // Set defaults for S3-compatible providers
            if (config.Region == null && string.IsNullOrEmpty(config.ServiceUrl))
                config.Region = RegionEndpoint.USEast1;
                
            return config;
        }

        /// <summary>
        /// Combines root path with object key if root path is specified
        /// </summary>
        public static string CombinePath(string? rootPath, string objectKey)
        {
            if (string.IsNullOrEmpty(rootPath))
                return objectKey;

            // Ensure root path doesn't end with slash and object key doesn't start with slash
            rootPath = rootPath.TrimEnd('/');
            objectKey = objectKey.TrimStart('/');

            return string.IsNullOrEmpty(objectKey) ? rootPath : $"{rootPath}/{objectKey}";
        }

        /// <summary>
        /// Extracts bucket name from S3 path
        /// </summary>
        public static string? GetBucketName(string s3Path)
        {
            if (string.IsNullOrEmpty(s3Path))
                return null;
            
            var segments = s3Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : null;
        }

        /// <summary>
        /// Extracts object key from S3 path (everything after bucket name)
        /// </summary>
        public static string? GetObjectKey(string s3Path)
        {
            if (string.IsNullOrEmpty(s3Path))
                return null;
            
            var segments = s3Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
                return null;
            
            // Join all segments except the first one (bucket name) to form the object key
            return string.Join("/", segments, 1, segments.Length - 1);
        }
    }
}
