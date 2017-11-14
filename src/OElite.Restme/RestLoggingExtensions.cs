using System;
using Microsoft.Extensions.Logging;

namespace OElite
{
	public partial class Rest
	{
		private ILogger Logger { get; set; }

		public void LogError(string errorMessage, Exception ex = null, int eventId = 0)
		{
			Logger?.LogError(eventId, ex, errorMessage);
		}
		public void LogInfo(string info, Exception ex = null, int eventId = 0)
		{
			Logger?.LogInformation(eventId, ex, info);
		}
		public void LogDebug(string debugInfo, Exception ex = null, int eventId = 0)
		{
			Logger?.LogDebug(eventId, debugInfo, ex);
		}
		public void LogFatal(string fatalInfo, Exception ex = null, int eventId = 0)
		{
			Logger?.LogCritical(eventId, fatalInfo, ex);
		}
	}
}
