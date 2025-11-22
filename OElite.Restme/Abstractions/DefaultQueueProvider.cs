using System;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Default queue provider that throws helpful error
/// </summary>
public class DefaultQueueProvider : IQueueProvider
{
    /// <summary>
    /// Provider name for debugging and logging
    /// </summary>
    public string ProviderName => "DefaultQueue";

    /// <summary>
    /// Configuration used to create this provider
    /// </summary>
    public RestConfig Configuration => new RestConfig();

    /// <summary>
    /// Capabilities supported by this provider
    /// </summary>
    public ProviderCapabilities Capabilities => ProviderCapabilities.Queue;

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

    // Cancellation token overloads
    public Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null,
        string? exchangeName = null, bool isDurable = true, bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true,
        CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }

    public Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler,
        Func<Task<bool>>? completionCondition = null, string? exchangeName = null,
        string? queueName = null, string? routingKey = null, ushort prefetchCount = 1,
        bool isDurable = true, bool isExclusive = false, bool autoDelete = true,
        string exchangeType = "direct", CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }

    public Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }

    public Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true,
        bool isExclusive = false, bool autoDelete = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }

    public Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct",
        bool isDurable = true, bool autoDelete = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }

    public Task BindQueueAsync(string queueName, string exchangeName, string routingKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.");
    }
}