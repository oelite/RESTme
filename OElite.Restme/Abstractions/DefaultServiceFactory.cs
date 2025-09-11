using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OElite.Abstractions
{
    /// <summary>
    /// Default service factory that provides fallback implementations
    /// This maintains backward compatibility when no specific providers are registered
    /// </summary>
    public class DefaultServiceFactory : IServiceFactory
    {
        private readonly ILogger? _logger;

        public DefaultServiceFactory(ILogger? logger = null)
        {
            _logger = logger;
        }

        public ICacheProvider CreateCacheProvider(string connectionString, RestConfig config)
        {
            // This will be implemented by the Redis provider project
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public IQueueProvider CreateQueueProvider(string connectionString, RestConfig config)
        {
            // This will be implemented by the RabbitMQ provider project
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public IStorageProvider CreateStorageProvider(string connectionString, RestConfig config)
        {
            // This will be implemented by the Azure/S3 provider projects
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public IHttpProvider CreateHttpProvider(RestConfig config)
        {
            // HTTP provider can be implemented in the main project or separate project
            return new DefaultHttpProvider(config, _logger);
        }

        public ILogProvider CreateLogProvider(RestConfig config)
        {
            return new DefaultLogProvider(_logger);
        }
    }

    /// <summary>
    /// Default HTTP provider implementation
    /// </summary>
    internal class DefaultHttpProvider : IHttpProvider
    {
        private readonly RestConfig _config;
        private readonly ILogger? _logger;

        public DefaultHttpProvider(RestConfig config, ILogger? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        public Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            // This will delegate to the existing HTTP implementation
            throw new NotImplementedException("HTTP provider implementation needed");
        }

        public Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            throw new NotImplementedException("HTTP provider implementation needed");
        }

        public Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null)
        {
            throw new NotImplementedException("HTTP provider implementation needed");
        }

        public Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            throw new NotImplementedException("HTTP provider implementation needed");
        }

        public Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            throw new NotImplementedException("HTTP provider implementation needed");
        }

        public Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null)
        {
            throw new NotImplementedException("HTTP provider implementation needed");
        }
    }

    /// <summary>
    /// Default log provider implementation
    /// </summary>
    public class DefaultLogProvider : ILogProvider
    {
        private readonly ILogger? _logger;

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
    }
}
