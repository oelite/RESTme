using System;
using OElite.Abstractions;

namespace OElite.Base
{
    /// <summary>
    /// Simple console logger provider without external dependencies.
    /// </summary>
    public sealed class ConsoleLogProvider : ILogProvider
    {
        private bool _disposed;

        public void LogError(string? message, Exception? exception = null, int eventId = 0)
        {
            Write("ERROR", message, exception, eventId);
        }

        public void LogWarning(string? message, Exception? exception = null, int eventId = 0)
        {
            Write("WARN", message, exception, eventId);
        }

        public void LogInformation(string? message, Exception? exception = null, int eventId = 0)
        {
            Write("INFO", message, exception, eventId);
        }

        public void LogDebug(string? message, Exception? exception = null, int eventId = 0)
        {
            Write("DEBUG", message, exception, eventId);
        }

        public void LogCritical(string? message, Exception? exception = null, int eventId = 0)
        {
            Write("CRIT", message, exception, eventId);
        }

        private static void Write(string level, string? message, Exception? ex, int eventId)
        {
            var ts = DateTime.UtcNow.ToString("o");
            Console.WriteLine($"[{ts}] [{level}] [event:{eventId}] {message}");
            if (ex != null)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}


