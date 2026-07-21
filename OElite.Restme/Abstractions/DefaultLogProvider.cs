using System;
using Microsoft.Extensions.Logging;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Default log provider implementation
/// </summary>
public class DefaultLogProvider : ILogProvider
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Provider name for debugging and logging
    /// </summary>
    public string ProviderName => "DefaultLog";

    /// <summary>
    /// Configuration used to create this provider
    /// </summary>
    public RestConfig Configuration => new(RestMode.LocalFileSystem);

    /// <summary>
    /// Capabilities supported by this provider
    /// </summary>
    public ProviderCapabilities Capabilities => ProviderCapabilities.None;

    public DefaultLogProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void LogError(string? message, Exception? exception = null, int eventId = 0)
    {
        _logger?.LogError(eventId, exception, message);
    }

    public void LogWarning(string? message, Exception? exception = null, int eventId = 0)
    {
        _logger?.LogWarning(eventId, exception, message);
    }

    public void LogInformation(string? message, Exception? exception = null, int eventId = 0)
    {
        _logger?.LogInformation(eventId, exception, message);
    }

    public void LogDebug(string? message, Exception? exception = null, int eventId = 0)
    {
        _logger?.LogDebug(eventId, exception, message);
    }

    public void LogCritical(string? message, Exception? exception = null, int eventId = 0)
    {
        _logger?.LogCritical(eventId, exception, message);
    }

    public void Dispose()
    {
        // Nothing to dispose for default logger
    }
}