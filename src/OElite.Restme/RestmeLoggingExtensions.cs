using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;

namespace OElite
{
    public partial class Restme
    {
        public static void LogError(string errorMessage, Exception ex, string loggerName = "General")
        {
            LogManager.GetLogger(loggerName).Error(ex, errorMessage);
        }
        public static void LogInfo(string info, Exception ex, string loggerName = "General")
        {
            LogManager.GetLogger(loggerName).Info(ex, info);
        }
        public static void LogDebug(string debugInfo, Exception ex, string loggerName = "General")
        {
            LogManager.GetLogger(loggerName).Debug(ex, debugInfo);
        }
        public static void LogFatal(string fatalInfo, Exception ex, string loggerName = "General")
        {
            LogManager.GetLogger(loggerName).Fatal(ex, fatalInfo);
        }
    }
}
