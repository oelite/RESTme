using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;

namespace OElite
{
    public partial class Rest
    {
        internal ConnectionMultiplexer? RedisConnection;
        internal IDatabase? RedisDatabase;

        private void PrepareRedisRestme()
        {
            try
            {
                RedisConnection = new Lazy<ConnectionMultiplexer?>(() =>
                {
                    ConnectionMultiplexer? result = null;
                    var redisConfig =
                        ConfigurationOptions.Parse(ConnectionString);
                    if (Configuration.DefaultTimeout > 0)
                    {
                        redisConfig.ConnectTimeout = Configuration.DefaultTimeout;
                        redisConfig.AsyncTimeout = Configuration.DefaultTimeout;
                        redisConfig.SyncTimeout = Configuration.DefaultTimeout;
                    }

                    bool success;
                    try
                    {
                        result = ConnectionMultiplexer.Connect(redisConfig);
                        success = result.IsConnected;
                    }
                    catch (Exception? ex)
                    {
                        LogError(ex.Message, ex);
                        success = false;
                    }

                    if (!success)
                    {
                        var endPoints = redisConfig.EndPoints;
                        foreach (var endpoint in endPoints.Cast<DnsEndPoint?>())
                        {
                            try
                            {
                                if (endpoint != null)
                                {
                                    var port = endpoint.Port;
                                    if (!IsIpAddress(endpoint.Host))
                                    {
                                        var ip = Dns.GetHostEntryAsync(endpoint.Host)!
                                            .WaitAndGetResult(Configuration.DefaultTimeout);
                                        redisConfig.EndPoints.Remove(endpoint);
                                        redisConfig.EndPoints.Add(ip?.AddressList.First(), port);
                                    }
                                }

                                result = ConnectionMultiplexer.Connect(redisConfig);
                            }
                            catch (Exception? innerEx)
                            {
                                LogError(innerEx.Message, innerEx);
                                continue;
                            }

                            if (result != null)
                                break;
                        }
                    }

                    return result;
                }).Value;
                RedisDatabase = RedisConnection?.GetDatabase();
            }
            catch (Exception? ex)
            {
                LogError(ex.Message, ex);
                throw new OEliteDbException("failed to initialize Redis connection:\n" + ex.Message, ex);
            }
            if (RedisConnection?.IsConnected == true)
                Initialized = true;

        }

        bool IsIpAddress(string host)
        {
            string ipPattern = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b";
            return Regex.IsMatch(host, ipPattern);
        }

        private Task AttemptDisposeRedis()
        {
            return Task.Run(() =>
            {
                if (RedisConnection == null) return;
                try
                {
                    RedisConnection.Dispose();
                }
                catch
                {
                    // ignored
                }
            });
        }
    }
}