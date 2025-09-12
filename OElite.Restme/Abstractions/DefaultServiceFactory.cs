using System;
using System.IO;
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

        public void Dispose()
        {
            // Nothing to dispose for default logger
        }
    }

    /// <summary>
    /// Default cache provider that throws helpful error
    /// </summary>
    public class DefaultCacheProvider : ICacheProvider
    {
        public void Dispose() { }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public Task<bool> RemoveAsync(string key)
        {
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public Task<bool> ExistsAsync(string key)
        {
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public Task<bool> SetExpiryAsync(string key, TimeSpan expiry)
        {
            throw new NotImplementedException("Redis provider not loaded. Please reference OElite.Restme.Redis package.");
        }

        public T? GetOriginalData<T>(ResponseMessage? responseMessage) where T : class
        {
            return responseMessage?.GetOriginalData<T>();
        }
    }

    /// <summary>
    /// Default queue provider that throws helpful error
    /// </summary>
    public class DefaultQueueProvider : IQueueProvider
    {
        public void Dispose() { }

        public Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null, 
            string? exchangeName = null, bool isDurable = true, bool isExclusive = false, 
            bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true) where T : class
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler, 
            Func<Task<bool>>? completionCondition = null, string? exchangeName = null,
            string? queueName = null, string? routingKey = null, ushort prefetchCount = 1,
            bool isDurable = true, bool isExclusive = false, bool autoDelete = true,
            string exchangeType = "direct") where T : class
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public Task StopConsumingAsync()
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true, 
            bool isExclusive = false, bool autoDelete = true)
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct", 
            bool isDurable = true, bool autoDelete = true)
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }

        public Task BindQueueAsync(string queueName, string exchangeName, string routingKey)
        {
            throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
        }
    }

    /// <summary>
    /// Default storage provider that throws helpful error
    /// </summary>
    public class DefaultStorageProvider : IStorageProvider
    {
        public void Dispose() { }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<T?> PutAsync<T>(string key, T value) where T : class
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<bool> DeleteAsync(string key)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<bool> ExistsAsync(string key)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<string?> GetStringAsync(string key)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<string?> PutStringAsync(string key, string value)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<Stream?> GetStreamAsync(string key)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<bool> PutStreamAsync(string key, Stream stream)
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }

        public Task<T?> GetStreamAsync<T>(string key) where T : Stream
        {
            throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
        }
    }
}
