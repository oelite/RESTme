using System;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for message queue operations (RabbitMQ, etc.)
    /// </summary>
    public interface IQueueProvider : IDisposable
    {
        /// <summary>
        /// Publish a message to a queue
        /// </summary>
        Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null, 
            string? exchangeName = null, bool isDurable = true, bool isExclusive = false, 
            bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true) where T : class;
        
        /// <summary>
        /// Start consuming messages from a queue
        /// </summary>
        Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler, 
            Func<Task<bool>>? completionCondition = null, string? exchangeName = null,
            string? queueName = null, string? routingKey = null, ushort prefetchCount = 1,
            bool isDurable = true, bool isExclusive = false, bool autoDelete = true,
            string exchangeType = "direct") where T : class;
        
        /// <summary>
        /// Stop consuming messages
        /// </summary>
        Task StopConsumingAsync();
        
        /// <summary>
        /// Declare a queue
        /// </summary>
        Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true, 
            bool isExclusive = false, bool autoDelete = true);
        
        /// <summary>
        /// Declare an exchange
        /// </summary>
        Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct", 
            bool isDurable = true, bool autoDelete = true);
        
        /// <summary>
        /// Bind a queue to an exchange
        /// </summary>
        Task BindQueueAsync(string queueName, string exchangeName, string routingKey);
    }
}
