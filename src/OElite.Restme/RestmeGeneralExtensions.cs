using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            if (restme.BaseUri != null)
                restme.CurrentMode = RestMode.HTTPClient;
            else
            {
                if (restme.ConnectionString.IsNotNullOrEmpty())
                {
                    var connectionString = restme.ConnectionString.ToLower();
                    if ((connectionString.Contains("defaultendpointsprotocol") &&
                        connectionString.Contains("accountname") &&
                        connectionString.Contains("accountkey")) ||
                        (connectionString.Contains("usedevelopmentstorage") &&
                        connectionString.Contains("true")
                        ))
                        restme.CurrentMode = RestMode.AzureStorageClient;
                    else if (restme.ConnectionString.ToLower().Contains("redis.cache.windows.net"))
                        restme.CurrentMode = RestMode.RedisCacheClient;
                }
                else
                    throw new NotSupportedException("Unable to identify RestmeMode.");
            }
        }
    }
}
