using System;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeRedisExtensions
    {
        public static async Task<T> RedisGetAsync<T>(this Rest restme, string redisKey)
        {
            MustBeRedisMode(restme);
            if (redisKey.IsNullOrEmpty())
            {
                restme.LogWarning($" RestmeRedis - Empty Redis Key identified which is unusual.  {redisKey}");
            }

            string stringValue = await restme.redisDatabase.StringGetAsync(redisKey);

            if (!stringValue.IsNotNullOrEmpty()) return default(T);
            if (typeof(T).IsPrimitiveType())
            {
                return (T)Convert.ChangeType(stringValue, typeof(T));
            }

            return stringValue.JsonDeserialize<T>(restme.Configuration.UseRestConvertForCollectionSerialization);
        }

        public static async Task<T> RedisPostAsync<T>(this Rest restme, string redisKey, object dataObject)
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

                if (await restme.redisDatabase.StringSetAsync(redisKey, objectInString))
                    return (T)dataObject;
            }
            else
            {
                restme.LogWarning($" RestmeRedis - Empty Redis Key identified in Redis Set.  {redisKey}");
            }

            return default(T);
        }

        public static async Task<T> RedisDeleteAsync<T>(this Rest restme, string redisKey)
        {
            MustBeRedisMode(restme);
            var result = await restme.redisDatabase.KeyDeleteAsync(redisKey);
            if (typeof(T) == typeof(bool))
                return (T)Convert.ChangeType(result, typeof(T));
            return default(T);
        }

        #region Private Methods

        private static void MustBeRedisMode(Rest restme)
        {
            if (restme?.CurrentMode != RestMode.RedisCacheClient)
                throw new InvalidOperationException(
                    $"current request is not valid operation, you are under RestMode: {restme.CurrentMode.ToString()}");
        }

        #endregion
    }
}