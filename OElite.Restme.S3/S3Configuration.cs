using System;
using Amazon;
using Amazon.S3;
using OElite.Restme;

namespace OElite.Providers
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
        /// Supports both URI format (s3://host:port/bucket) and key=value format
        /// </summary>
        public static S3Configuration ParseConnectionString(RestConfig restConfig)
        {
            if (string.IsNullOrEmpty(restConfig.ConnectionString))
                throw new ArgumentException("Connection string cannot be null or empty",
                    nameof(restConfig.ConnectionString));

            var config = new S3Configuration();

            // Check if this is a URI-style connection string
            if (restConfig.ConnectionString.StartsWith("s3://") || restConfig.ConnectionString.StartsWith("s3s://"))
            {
                return ParseS3Uri(restConfig.ConnectionString);
            }

            // Parse key=value style connection string
            var parts = restConfig.ConnectionString.Split(';');

            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "accesskeyid":
                    case "accesskey":
                        config.AccessKeyId = value;
                        break;
                    case "secretaccesskey":
                    case "secretkey":
                        config.SecretAccessKey = value;
                        break;
                    case "region":
                        config.Region = RegionEndpoint.GetBySystemName(value);
                        break;
                    case "bucketname":
                    case "bucket":
                        config.BucketName = value;
                        if (restConfig.InstanceName.IsNotNullOrEmpty())
                        {
                            config.BucketName = restConfig.InstanceName;
                        }

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
            if (config.Region == null)
            {
                // Always set a region - required by AWS SDK
                config.Region = RegionEndpoint.USEast1;
            }

            return config;
        }

        /// <summary>
        /// Parses URI-style S3 connection string (e.g., s3://host:port/bucket)
        /// </summary>
        private static S3Configuration ParseS3Uri(string connectionString)
        {
            var config = new S3Configuration();

            try
            {
                var uri = new Uri(connectionString);
                var useHttps = uri.Scheme == "s3s";

                // Build service URL
                var port = uri.Port > 0 ? uri.Port : (useHttps ? 443 : 80);
                var protocol = useHttps ? "https" : "http";
                config.ServiceUrl = $"{protocol}://{uri.Host}:{port}";
                config.UseHttp = !useHttps;
                config.ForcePathStyle = true; // Required for MinIO and custom endpoints

                // Extract bucket name from path
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                {
                    var pathSegments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (pathSegments.Length > 0)
                    {
                        config.BucketName = pathSegments[0];

                        // If there are more path segments, use them as root path
                        if (pathSegments.Length > 1)
                        {
                            config.RootPath = string.Join("/", pathSegments, 1, pathSegments.Length - 1);
                        }
                    }
                }

                // Set default region for custom endpoints
                config.Region = RegionEndpoint.USEast1;

                return config;
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException($"Invalid S3 URI format: {connectionString}",
                    nameof(connectionString), ex);
            }
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