using System;
using System.Text;
using System.Threading;
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
        private readonly IChannel _channel;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public RabbitMQProvider(string connectionString, RestConfig config)
        {
            _config = config;
            
            var factory = new ConnectionFactory();
            
            // Check if connection string includes VHost info (format: uri|vhost=name)
            string actualConnectionString = connectionString;
            string? vhost = null;
            
            if (connectionString.Contains("|vhost="))
            {
                var parts = connectionString.Split(new[] { "|vhost=" }, StringSplitOptions.None);
                actualConnectionString = parts[0];
                vhost = parts.Length > 1 ? parts[1] : null;
            }
            
            // Parse the connection string/URI
            if (actualConnectionString.StartsWith("amqp://") || actualConnectionString.StartsWith("amqps://"))
            {
                factory.Uri = new Uri(actualConnectionString);
            }
            else
            {
                // Parse connection string format: host:port or host
                var parts = actualConnectionString.Split(':');
                factory.HostName = parts[0];
                if (parts.Length > 1 && int.TryParse(parts[1], out int port))
                {
                    factory.Port = port;
                }
            }
            
            // Override with authentication from RestConfig if provided
            if (!string.IsNullOrEmpty(config.RestKey))
            {
                factory.UserName = config.RestKey;
            }
            
            if (!string.IsNullOrEmpty(config.RestSecret))
            {
                factory.Password = config.RestSecret;
            }
            
            // Set VHost if provided
            if (!string.IsNullOrEmpty(vhost))
            {
                factory.VirtualHost = vhost;
            }

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        }


        public async Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null,
            string? exchangeName = null, bool isDurable = true, bool isExclusive = false,
            bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true,
            CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                if (string.IsNullOrEmpty(exchangeName))
                    exchangeName = "";

                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(queueName))
                    queueName = await DeclareQueueAsync(cancellationToken: cancellationToken);

                if (string.IsNullOrEmpty(routingKey))
                    routingKey = queueName;

                // Declare exchange if provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await DeclareExchangeAsync(exchangeName, exchangeType, isDurable, autoDelete, cancellationToken);
                }

                // Declare queue
                await DeclareQueueAsync(queueName, isDurable, isExclusive, autoDelete, cancellationToken);

                // Bind queue to exchange if exchange is provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await BindQueueAsync(queueName, exchangeName, routingKey, cancellationToken);
                }

                var jsonMessage = message.JsonSerialize(_config.UseRestConvertForCollectionSerialization, _config.SerializerSettings);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                var properties = new BasicProperties
                {
                    Persistent = isMessagePersistent
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, mandatory: false, basicProperties: properties, body: body, cancellationToken);

                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to publish message: {ex.Message}", ex);
            }
        }


        public async Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler,
            Func<Task<bool>>? completionCondition = null, string? exchangeName = null,
            string? queueName = null, string? routingKey = null, ushort prefetchCount = 1,
            bool isDurable = true, bool isExclusive = false, bool autoDelete = true,
            string exchangeType = "direct", CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(queueName))
                    queueName = await DeclareQueueAsync(cancellationToken: cancellationToken);

                if (string.IsNullOrEmpty(routingKey))
                    routingKey = queueName;

                // Declare exchange if provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await DeclareExchangeAsync(exchangeName, exchangeType, isDurable, autoDelete, cancellationToken);
                }

                // Always declare the queue before binding (auto-create if missing)
                await DeclareQueueAsync(queueName, isDurable, isExclusive, autoDelete, cancellationToken);

                // Bind queue to exchange if exchange is provided
                if (!string.IsNullOrEmpty(exchangeName))
                {
                    await BindQueueAsync(queueName, exchangeName, routingKey, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await _channel.BasicQosAsync(0, prefetchCount, false, cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var deserializedMessage = message.JsonDeserialize<T>();

                        var success = await messageHandler(deserializedMessage);

                        if (success)
                        {
                            await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken);
                        }
                        else
                        {
                            await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                        }

                        // Check completion condition
                        if (completionCondition != null && await completionCondition())
                        {
                            await _channel.BasicCancelAsync(ea.ConsumerTag, false, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken);
                        throw new OEliteException($"Error processing message: {ex.Message}", ex);
                    }
                };

                await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to start consuming: {ex.Message}", ex);
            }
        }


        public async Task StopConsumingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Close the channel and connection
                cancellationToken.ThrowIfCancellationRequested();
                if (_channel.IsOpen)
                    await _channel.CloseAsync();

                if (_connection.IsOpen)
                    await _connection.CloseAsync();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to stop consuming: {ex.Message}", ex);
            }
        }


        public async Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true,
            bool isExclusive = false, bool autoDelete = true, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                    queueName = $"queue_{Guid.NewGuid():N}";

                cancellationToken.ThrowIfCancellationRequested();
                // Use passive=false to auto-create the queue if it doesn't exist
                await _channel.QueueDeclareAsync(queue: queueName, durable: isDurable, exclusive: isExclusive,
                    autoDelete: autoDelete, arguments: null, passive: false, cancellationToken: cancellationToken);

                return queueName;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // Log but don't fail if queue already exists with different parameters
                if (ex.Message.Contains("PRECONDITION_FAILED"))
                {
                    // Queue exists but with different parameters - this is usually OK for consumers
                    return queueName ?? "";
                }
                throw new OEliteException($"Failed to declare queue '{queueName}': {ex.Message}", ex);
            }
        }


        public async Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct",
            bool isDurable = true, bool autoDelete = true, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Use passive=false to auto-create the exchange if it doesn't exist
                await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: exchangeType, durable: isDurable,
                    autoDelete: autoDelete, arguments: null, passive: false, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // Log but don't fail if exchange already exists with different parameters
                if (ex.Message.Contains("PRECONDITION_FAILED"))
                {
                    // Exchange exists but with different parameters - this is usually OK
                    return;
                }
                throw new OEliteException($"Failed to declare exchange '{exchangeName}': {ex.Message}", ex);
            }
        }


        public async Task BindQueueAsync(string queueName, string exchangeName, string routingKey, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new OEliteException($"Failed to bind queue '{queueName}' to exchange '{exchangeName}': {ex.Message}", ex);
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
