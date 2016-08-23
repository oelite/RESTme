using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
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
					try
					{
						result = ConnectionMultiplexer.Connect(this.ConnectionString);
					}
					catch (AggregateException ex)
					{
						if (ex.Message.Contains("DNS endpoints") || ex.InnerException is PlatformNotSupportedException)
						{
							var addresses = Task.Run(async () => await System.Net.Dns.GetHostAddressesAsync(this.ConnectionString.Split(new char[] { ',', ':' })[0])).Result;
							if (addresses?.Length > 0)
							{
								foreach (var address in addresses)
								{
									var ip4Address = address.MapToIPv4();
									try
									{
										result = Task.Run(async () => await ConnectionMultiplexer.ConnectAsync(ip4Address?.ToString() + this.ConnectionString.Substring(this.ConnectionString.IndexOf(':')))).Result;
									}
									catch { }
									if (result != null)
										break;
								}
							}

						}

					}
					return result;
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
