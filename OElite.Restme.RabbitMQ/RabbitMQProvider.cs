using System;
using System.Text;
using System.Threading.Tasks;
using OElite.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OElite.Providers
{
    /// <summary>
    /// RabbitMQ implementation of IQueueProvider
    /// </summary>
    public class RabbitMQProvider : IQueueProvider
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public RabbitMQProvider(string connectionString, RestConfig config)
        {
            _config = config;
            
            var factory = new ConnectionFactory();
            if (connectionString.StartsWith("amqp://") || connectionString.StartsWith("amqps://"))
            {
                factory.Uri = new Uri(connectionString);
            }
            else
            {
                // Parse connection string format: host:port or host
                var parts = connectionString.Split(':');
                factory.HostName = parts[0];
                if (parts.Length > 1 && int.TryParse(parts[1], out int port))
                {
                    factory.Port = port;
                }
            }

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        public async Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null, 
            string? exchangeName = null, bool isDurable = true, bool isExclusive = false, 
            bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true) where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(exchangeName))
                    exchangeName = "";

                if (string.IsNullOrEmpty(queueName))
                    queueName = DeclareQueueAsync().Result;

                if (string.IsNullOrEmpty(routingKey))
                    routingKey = queueName;

                // Declare exchange if provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await DeclareExchangeAsync(exchangeName, exchangeType, isDurable, autoDelete);
                }

                // Declare queue
                await DeclareQueueAsync(queueName, isDurable, isExclusive, autoDelete);

                // Bind queue to exchange if exchange is provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await BindQueueAsync(queueName, exchangeName, routingKey);
                }

                var jsonMessage = message.JsonSerialize(_config.UseRestConvertForCollectionSerialization, _config.SerializerSettings);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = isMessagePersistent;

                _channel.BasicPublish(exchange: exchangeName, routingKey: routingKey, basicProperties: properties, body: body);
                
                return true;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to publish message: {ex.Message}", ex);
            }
        }

        public async Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler, 
            Func<Task<bool>>? completionCondition = null, string? exchangeName = null,
            string? queueName = null, string? routingKey = null, ushort prefetchCount = 1,
            bool isDurable = true, bool isExclusive = false, bool autoDelete = true,
            string exchangeType = "direct") where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                    queueName = await DeclareQueueAsync();

                if (string.IsNullOrEmpty(routingKey))
                    routingKey = queueName;

                // Declare exchange if provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await DeclareExchangeAsync(exchangeName, exchangeType, isDurable, autoDelete);
                    await BindQueueAsync(queueName, exchangeName, routingKey);
                }

                _channel.BasicQos(0, prefetchCount, false);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var deserializedMessage = message.JsonDeserialize<T>();

                        var success = await messageHandler(deserializedMessage);
                        
                        if (success)
                        {
                            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        else
                        {
                            _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                        }

                        // Check completion condition
                        if (completionCondition != null && await completionCondition())
                        {
                            _channel.BasicCancel(ea.ConsumerTag);
                        }
                    }
                    catch (Exception ex)
                    {
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                        throw new OEliteWebException($"Error processing message: {ex.Message}", ex);
                    }
                };

                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to start consuming: {ex.Message}", ex);
            }
        }

        public async Task StopConsumingAsync()
        {
            try
            {
                // Close the channel and connection
                if (_channel.IsOpen)
                    _channel.Close();
                
                if (_connection.IsOpen)
                    _connection.Close();
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to stop consuming: {ex.Message}", ex);
            }
        }

        public async Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true, 
            bool isExclusive = false, bool autoDelete = true)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                    queueName = $"queue_{Guid.NewGuid():N}";

                _channel.QueueDeclare(queue: queueName, durable: isDurable, exclusive: isExclusive, 
                    autoDelete: autoDelete, arguments: null);
                
                return queueName;
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to declare queue '{queueName}': {ex.Message}", ex);
            }
        }

        public async Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct", 
            bool isDurable = true, bool autoDelete = true)
        {
            try
            {
                _channel.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: isDurable, 
                    autoDelete: autoDelete, arguments: null);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to declare exchange '{exchangeName}': {ex.Message}", ex);
            }
        }

        public async Task BindQueueAsync(string queueName, string exchangeName, string routingKey)
        {
            try
            {
                _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: routingKey);
            }
            catch (Exception ex)
            {
                throw new OEliteWebException($"Failed to bind queue '{queueName}' to exchange '{exchangeName}': {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _channel?.Dispose();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
