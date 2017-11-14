﻿using System;
using System.Threading.Tasks;
using OElite.Utils;

namespace OElite
{
    public static class RestmeRedisExtensions
    {
        public static async Task<T> RedisGetAsync<T>(this Rest restme, string redisKey)
        {
            MustBeRedisMode(restme);
            string stringValue = await restme.redisDatabase.StringGetAsync(redisKey);

            if (stringValue.IsNotNullOrEmpty())
            {
                if (typeof(T).IsPrimitiveType())
                {
                    return (T) Convert.ChangeType(stringValue, typeof(T));
                }
                return stringValue.JsonDeserialize<T>(restme.Configuration.UseRestConvertForCollectionSerialization);
            }
            else
                return default(T);
        }

        public static async Task<T> RedisPostAsync<T>(this Rest restme, string redisKey, T dataObject)
        {
            MustBeRedisMode(restme);
            if (await restme.redisDatabase.StringSetAsync(redisKey, dataObject.JsonSerialize(
                restme.Configuration.UseRestConvertForCollectionSerialization)))
                return dataObject;
            return default(T);
        }

        public static async Task<T> RedisDeleteAsync<T>(this Rest restme, string redisKey)
        {
            MustBeRedisMode(restme);
            var result = await restme.redisDatabase.KeyDeleteAsync(redisKey);
            if (typeof(T) == typeof(bool))
                return (T) Convert.ChangeType(result, typeof(T));
            else
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