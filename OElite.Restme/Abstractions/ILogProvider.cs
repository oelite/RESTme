using System;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for logging operations
    /// </summary>
    public interface ILogProvider : IDisposable
    {
        /// <summary>
        /// Log error message
        /// </summary>
        void LogError(string? message, Exception? exception = null, int eventId = 0);
        
        /// <summary>
        /// Log warning message
        /// </summary>
        void LogWarning(string? message, Exception? exception = null, int eventId = 0);
        
        /// <summary>
        /// Log information message
        /// </summary>
        void LogInformation(string? message, Exception? exception = null, int eventId = 0);
        
        /// <summary>
        /// Log debug message
        /// </summary>
        void LogDebug(string? message, Exception? exception = null, int eventId = 0);
        
        /// <summary>
        /// Log critical message
        /// </summary>
        void LogCritical(string? message, Exception? exception = null, int eventId = 0);
    }
}
