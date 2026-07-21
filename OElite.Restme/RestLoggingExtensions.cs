using System;
using Microsoft.Extensions.Logging;
using OElite.Restme.Abstractions;

namespace OElite.Restme
{
    public partial class Rest
    {
        private ILogger? Logger { get; set; }

        public void LogError(string? errorMessage, Exception? ex = null, int eventId = 0)
        {
            try
            {
                var logProvider = GetProvider<ILogProvider>();
                if (logProvider != null)
                {
                    logProvider.LogError(errorMessage, ex, eventId);
                }
                else
                {
                    Logger?.LogError(eventId, ex, errorMessage ?? "empty error message");
                }
            }
            catch
            {
                // ignored
            }
        }

        public void LogWarning(string? errorMessage, Exception? ex = null, int eventId = 0)
        {
            try
            {
                var logProvider = GetProvider<ILogProvider>();
                if (logProvider != null)
                {
                    logProvider.LogWarning(errorMessage, ex, eventId);
                }
                else
                {
                    Logger?.LogWarning(eventId, ex, errorMessage ?? "empty error message");
                }
            }
            catch
            {
                // ignored
            }
        }

        public void LogInfo(string? info, Exception? ex = null, int eventId = 0)
        {
            try
            {
                var logProvider = GetProvider<ILogProvider>();
                if (logProvider != null)
                {
                    logProvider.LogInformation(info, ex, eventId);
                }
                else
                {
                    Logger?.LogInformation(eventId, ex, info ?? "empty info");
                }
            }
            catch
            {
                // ignored
            }
        }

        public void LogDebug(string? debugInfo, Exception? ex = null, int eventId = 0)
        {
            try
            {
                var logProvider = GetProvider<ILogProvider>();
                if (logProvider != null)
                {
                    logProvider.LogDebug(debugInfo, ex, eventId);
                }
                else
                {
                    Logger?.LogDebug(eventId, debugInfo ?? "empty debug info", ex);
                }
            }
            catch
            {
                // ignored
            }
        }

        public void LogFatal(string? fatalInfo, Exception? ex = null, int eventId = 0)
        {
            try
            {
                var logProvider = GetProvider<ILogProvider>();
                if (logProvider != null)
                {
                    logProvider.LogCritical(fatalInfo, ex, eventId);
                }
                else
                {
                    Logger?.LogCritical(eventId, fatalInfo ?? "empty fatal info", ex);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}