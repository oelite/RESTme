using System.Diagnostics;

namespace OElite
{
    public static class RestmeGeneralExtensions
    {
        /// <summary>
        /// Use base uri to identify the use of current Restme client
        /// </summary>
        /// <param name="restme"></param>
        public static void PrepareRestMode(this Rest restme)
        {
            restme.CurrentMode = restme.Configuration.OperationMode;

            if (!restme.ConnectionString.IsNotNullOrEmpty()) return;
            Debug.Assert(restme.ConnectionString != null, "restme.ConnectionString != null");
            var connectionString = restme.ConnectionString.ToLower();
            if ((connectionString.Contains("defaultendpointsprotocol") &&
                 connectionString.Contains("accountname") &&
                 connectionString.Contains("accountkey")) ||
                (connectionString.Contains("usedevelopmentstorage") &&
                 connectionString.Contains("true")
                ))
            {
                restme.CurrentMode = RestMode.AzureAsStorage;
            }
            else if (restme.ConnectionString.ToLower().Contains("redis.cache.windows.net") ||
                     restme.ConnectionString.ToLower().Contains(":6379") ||
                     restme.ConnectionString.ToLower().Contains(":6380"))
            {
                restme.CurrentMode = RestMode.RedisAsCache;
            }
            else if (connectionString.IsS3Provider())
            {
                restme.CurrentMode = RestMode.S3AsStorage;
            }
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// Check if connection string is for S3 provider
        /// </summary>
        public static bool IsS3Provider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;
            
            var lower = connectionString.ToLower();
            return lower.Contains("amazonaws.com") || 
                   lower.Contains("s3://") || 
                   lower.Contains("accesskeyid") && lower.Contains("secretaccesskey");
        }
    }
}