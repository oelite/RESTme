using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeRedisExtensions
    {
        public static async Task<T?> RedisGetAsync<T>(this Rest restme, string? redisKey)
        {
            MustBeRedisMode(restme);
            if (redisKey != null && redisKey.IsNullOrEmpty())
            {
                restme.LogWarning($" RestmeRedis - Empty Redis Key identified which is unusual.  {redisKey}");
            }

            Debug.Assert(restme.RedisDatabase != null, "restme.RedisDatabase != null");
            string stringValue = await restme.RedisDatabase.StringGetAsync(redisKey);

            if (!stringValue.IsNotNullOrEmpty()) return default;
            if (typeof(T).IsPrimitiveType())
            {
                return (T)Convert.ChangeType(stringValue, typeof(T));
            }

            return stringValue.JsonDeserialize<T>(restme.Configuration.UseRestConvertForCollectionSerialization);
        }

        public static async Task<T?> RedisPostAsync<T>(this Rest restme, string? redisKey, object? dataObject,
            TimeSpan? expiryInMinutes = null)
        {
            MustBeRedisMode(restme);
            if (redisKey.IsNotNullOrEmpty())
            {
                var objectInString = dataObject == null
                    ? string.Empty
                    : dataObject.JsonSerialize(restme.Configuration.UseRestConvertForCollectionSerialization);

                if (objectInString.IsNullOrEmpty() && dataObject != null && dataObject is not string)
                {
                    restme.LogWarning(
                        $" RestmeRedis - Empty object identified in Redis Set whilst the object is type of {dataObject.GetType().Name}.  {redisKey}");
                }

                Debug.Assert(restme.RedisDatabase != null, "restme.RedisDatabase != null");
                if (await restme.RedisDatabase.StringSetAsync(redisKey, objectInString, expiryInMinutes))
                    return (T?)dataObject;
            }
            else
            {
                restme.LogWarning($" RestmeRedis - Empty Redis Key identified in Redis Set.  {redisKey}");
            }

            return default;
        }

        public static async Task<T?> RedisDeleteAsync<T>(this Rest restme, string? redisKey)
        {
            MustBeRedisMode(restme);
            Debug.Assert(restme.RedisDatabase != null, "restme.RedisDatabase != null");
            var result = await restme.RedisDatabase.KeyDeleteAsync(redisKey);
            if (typeof(T) == typeof(bool))
                return (T)Convert.ChangeType(result, typeof(T));
            return default;
        }

        #region Private Methods

        private static void MustBeRedisMode(Rest restme)
        {
            if (restme.CurrentMode != RestMode.RedisCacheClient)
                throw new InvalidOperationException(
                    $"current request is not valid operation, you are under RestMode: {restme.CurrentMode.ToString()}");
        }

        #endregion
    }
}