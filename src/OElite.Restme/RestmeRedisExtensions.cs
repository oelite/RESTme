using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeRedisExtensions
    {
        public static async Task<T> RedisGetAsync<T>(this Restme restme, string key)
        {
            //make sure relativePath from caller will be parsed into key name correctly;
            return default(T);
        }
    }
}
