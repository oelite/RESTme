using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OElite
{
    public partial class Restme
    {
        internal ConnectionMultiplexer redisConnection;
        internal IDatabase redisDatabase;

        private void PrepareRedisRestme()
        {
            try
            {
                redisConnection = new Lazy<ConnectionMultiplexer>(() =>
                {
                    return ConnectionMultiplexer.Connect(this.ConnectionString);
                }).Value;
                redisDatabase = redisConnection.GetDatabase();
            }
            catch (Exception ex)
            {
                throw new OEliteDbException("failed to initialize Redis connection:\n" + ex.Message, ex);
            }
        }



    }
}
