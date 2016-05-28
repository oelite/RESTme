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
        public static void PrepareRestMode(this Restme restme)
        {
            var baseHost = restme.BaseUri?.Host?.ToLower();
            if (baseHost.IsNotNullOrEmpty())
            {
                if (baseHost.Contains("blob.core.windows.net"))
                    restme.CurrentMode = RestMode.AzureStorageClient;
                else if (baseHost.Contains("redis.cache.windows.net"))
                    restme.CurrentMode = RestMode.RedisCacheClient;
                else
                    restme.CurrentMode = RestMode.HTTPClient;
            }

        }
    }
}
