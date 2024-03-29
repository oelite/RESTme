﻿using System;
using Microsoft.Extensions.Logging;

namespace OElite
{
    public partial class Rest
    {
        private ILogger Logger { get; set; }

        public void LogError(string errorMessage, Exception ex = null, int eventId = 0)
        {
            try
            {
                Logger?.LogError(eventId, ex, errorMessage ?? "empty error message");
            }
            catch
            {
            }
        }

        public void LogWarning(string errorMessage, Exception ex = null, int eventId = 0)
        {
            try
            {
                Logger?.LogWarning(eventId, ex, errorMessage ?? "empty error message");
            }
            catch
            {
            }
        }

        public void LogInfo(string info, Exception ex = null, int eventId = 0)
        {
            try
            {
                Logger?.LogInformation(eventId, ex, info ?? "empty info");
            }
            catch
            {
            }
        }

        public void LogDebug(string debugInfo, Exception ex = null, int eventId = 0)
        {
            try
            {
                Logger?.LogDebug(eventId, debugInfo ?? "empty debug info", ex);
            }
            catch
            {
            }
        }

        public void LogFatal(string fatalInfo, Exception ex = null, int eventId = 0)
        {
            try
            {
                Logger?.LogCritical(eventId, fatalInfo ?? "empty fatal info", ex);
            }
            catch
            {
            }
        }
    }
}