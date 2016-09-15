using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OElite
{
    public partial class Rest
    {
        internal ConnectionMultiplexer redisConnection;
        internal IDatabase redisDatabase;

        private void PrepareRedisRestme()
        {
            try
            {
                redisConnection = new Lazy<ConnectionMultiplexer>(() =>
                {
                    ConnectionMultiplexer result = null;
                    var redisConfig =
                        ConfigurationOptions.Parse(this.ConnectionString);
                    try
                    {
                        result = ConnectionMultiplexer.Connect(redisConfig);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex.Message, ex);
                        var host = this.ConnectionString.Split(new char[] {',', ':'})[0];
                        var hostEntry = Task.Run(async () => await System.Net.Dns.GetHostEntryAsync(host)).
                            Result;
                        var addresses =
                            hostEntry?.AddressList?.Where(item => item.AddressFamily == AddressFamily.InterNetwork)?
                                .ToArray();
                        if (!(addresses?.Length > 0)) return result;

                        foreach (var address in addresses)
                        {
                            var ip4Address = address.MapToIPv4();
                            try
                            {
                                redisConfig = ConfigurationOptions.Parse(ip4Address +
                                                                         ConnectionString.Substring(
                                                                             ConnectionString.IndexOf(':')));
                                result = ConnectionMultiplexer.Connect(redisConfig);
                            }
                            catch (Exception innerEx)
                            {
                                LogError(innerEx.Message, innerEx);
                                continue;
                            }
                            if (result != null)
                            {
                                break;
                            }
                        }
                    }
                    return result;
                }).Value;
                redisDatabase = redisConnection.GetDatabase();
            }
            catch (Exception ex)
            {
                LogError(ex.Message, ex);
                throw new OEliteDbException("failed to initialize Redis connection:\n" + ex.Message, ex);
            }
        }
    }
}