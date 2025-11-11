using System;

namespace OElite.Utils
{
    /// <summary>
    /// Azure configuration model for parsing connection strings
    /// </summary>
    public class AzureConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string? RootPath { get; set; }
    }

    /// <summary>
    /// Utility class for parsing Azure connection strings
    /// </summary>
    public static class AzureConnectionStringParser
    {
        /// <summary>
        /// Parses Azure connection string into configuration object
        /// </summary>
        public static AzureConfiguration ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            var config = new AzureConfiguration();
            var parts = connectionString.Split(';');
            
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length != 2) continue;
                
                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();
                
                switch (key)
                {
                    case "rootpath":
                        config.RootPath = value;
                        break;
                    default:
                        // For Azure, we need to preserve the original connection string format
                        // So we'll store the full connection string and let the Azure SDK handle it
                        break;
                }
            }
            
            config.ConnectionString = connectionString;
            return config;
        }

        /// <summary>
        /// Combines root path with blob path if root path is specified
        /// </summary>
        public static string CombinePath(string? rootPath, string blobPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                return blobPath;

            // Ensure root path doesn't end with slash and blob path doesn't start with slash
            rootPath = rootPath.TrimEnd('/');
            blobPath = blobPath.TrimStart('/');

            return string.IsNullOrEmpty(blobPath) ? rootPath : $"{rootPath}/{blobPath}";
        }

        /// <summary>
        /// Extracts container name from Azure blob path
        /// </summary>
        public static string GetContainerName(string storageRelativePath)
        {
            if (string.IsNullOrEmpty(storageRelativePath))
                throw new ArgumentException("Storage relative path cannot be null or empty.");

            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : "default";
        }

        /// <summary>
        /// Extracts blob item path from Azure blob path (everything after container name)
        /// </summary>
        public static string? GetBlobItemPath(string storageRelativePath)
        {
            if (string.IsNullOrEmpty(storageRelativePath))
                return null;

            var segments = storageRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
                return null;

            return string.Join("/", segments, 1, segments.Length - 1);
        }
    }
}
