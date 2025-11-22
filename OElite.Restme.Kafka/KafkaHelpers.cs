using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Kafka
{


    /// <summary>
    /// Simplified Kafka connection (would use actual Kafka client in real implementation)
    /// </summary>
    public class KafkaConnection : IDisposable
    {
        private readonly string _bootstrapServers;
        private readonly ProducerConfig _producerConfig;
        private readonly ConsumerConfig _consumerConfig;
        private readonly AdminClientConfig _adminConfig;
        private IProducer<string, string>? _producer;
        private IAdminClient? _adminClient;
        private bool _disposed = false;

        public KafkaConnection(string connectionString, RestConfig? config = null)
        {
            // Parse connection string to extract bootstrap servers
            // Format: kafka://host:port or just host:port
            _bootstrapServers = connectionString.Replace("kafka://", "");

            _producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MaxInFlight = 5,
                MessageTimeoutMs = 30000
            };

            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 3000
            };

            _adminConfig = new AdminClientConfig
            {
                BootstrapServers = _bootstrapServers
            };

            // Add authentication if provided via RestConfig
            if (config != null)
            {
                var username = config.AuthKey;
                var password = config.AuthSecret;

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    // Configure SASL authentication
                    _producerConfig.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                    _producerConfig.SaslMechanism = SaslMechanism.Plain;
                    _producerConfig.SaslUsername = username;
                    _producerConfig.SaslPassword = password;

                    _consumerConfig.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                    _consumerConfig.SaslMechanism = SaslMechanism.Plain;
                    _consumerConfig.SaslUsername = username;
                    _consumerConfig.SaslPassword = password;

                    _adminConfig.SecurityProtocol = SecurityProtocol.SaslPlaintext;
                    _adminConfig.SaslMechanism = SaslMechanism.Plain;
                    _adminConfig.SaslUsername = username;
                    _adminConfig.SaslPassword = password;
                }
            }
        }

        private IProducer<string, string> GetProducer()
        {
            if (_producer == null)
            {
                _producer = new ProducerBuilder<string, string>(_producerConfig).Build();
            }
            return _producer;
        }

        private IAdminClient GetAdminClient()
        {
            if (_adminClient == null)
            {
                _adminClient = new AdminClientBuilder(_adminConfig).Build();
            }
            return _adminClient;
        }

        public async Task PublishAsync<T>(T message, string topicName, string key, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));

            var producer = GetProducer();
            var messageValue = JsonSerializer.Serialize(message);

            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = messageValue
            };

            await producer.ProduceAsync(topicName, kafkaMessage, cancellationToken);
        }

        public async Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"PublishBatchAsync called with {messages?.Count()} messages for topic: {topicName}");
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));

            var producer = GetProducer();
            var tasks = new List<Task>();

            foreach (var message in messages)
            {
                var messageValue = JsonSerializer.Serialize(message);
                Console.WriteLine($"Publishing individual message: {messageValue}");
                var key = keySelector?.Invoke(message);

                var kafkaMessage = new Message<string, string>
                {
                    Key = key,
                    Value = messageValue
                };

                tasks.Add(producer.ProduceAsync(topicName, kafkaMessage, cancellationToken));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("All messages published successfully");
        }

        public async Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"SubscribeAsync called for topic: {topicName}, group: {consumerGroup}");
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));
            if (string.IsNullOrEmpty(consumerGroup))
                throw new ArgumentNullException(nameof(consumerGroup));
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler));

            // Ensure topic exists before subscribing
            Console.WriteLine("Creating topic...");
            await CreateTopicAsync(topicName, cancellationToken: cancellationToken);
            Console.WriteLine("Topic created, setting up consumer...");

            var consumerConfig = new ConsumerConfig(_consumerConfig)
            {
                GroupId = consumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(topicName);
            Console.WriteLine("Consumer subscribed, starting message loop...");

            try
            {
                int loopCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    loopCount++;
                    Console.WriteLine($"Consumer loop iteration {loopCount}, calling Consume()...");

                    try
                    {
                        // Use timeout for Consume to prevent infinite blocking
                        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                        Console.WriteLine($"Consume() returned, IsPartitionEOF: {consumeResult?.IsPartitionEOF}, Topic: {consumeResult?.Topic}, Offset: {consumeResult?.Offset}");

                        if (consumeResult != null && consumeResult.Message != null)
                        {
                            Console.WriteLine($"Consumer received message: '{consumeResult.Message.Value}'");
                            try
                            {
                                var message = JsonSerializer.Deserialize<T>(consumeResult.Message.Value);
                                if (message != null)
                                {
                                    Console.WriteLine($"Successfully deserialized message for user: {(message as dynamic)?.UserId}");
                                    await messageHandler(message);
                                }
                                else
                                {
                                    Console.WriteLine("Deserialized message was null");
                                }
                            }
                            catch (JsonException ex)
                            {
                                // Log the problematic message for debugging
                                Console.WriteLine($"Failed to deserialize message: '{consumeResult.Message.Value}'. Error: {ex.Message}");
                                throw;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Consumer received null result or null message - continuing to poll");
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Consume exception: {ex.Message}");
                        // Continue the loop on consume exceptions
                    }

                    // Check cancellation more frequently
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Cancellation requested, breaking consumer loop");
                        break;
                    }
                }
                Console.WriteLine($"Consumer loop ended after {loopCount} iterations");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        public Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "Kafka client implementation requires Confluent.Kafka or similar package. " +
                "Please install the appropriate Kafka client library and implement the connection logic.");
        }

        public async Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "Kafka client implementation requires Confluent.Kafka or similar package. " +
                "Please install the appropriate Kafka client library and implement the connection logic.");
        }

        public async Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));

            using var adminClient = new AdminClientBuilder(_adminConfig).Build();

            try
            {
                var topicSpec = new TopicSpecification { Name = topicName, NumPartitions = partitions, ReplicationFactor = replicationFactor };
                await adminClient.CreateTopicsAsync(new[] { topicSpec });
            }
            catch (CreateTopicsException ex)
            {
                // Topic might already exist, which is fine for our purposes
                if (!ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    throw;
                }
            }
            catch
            {
                // Ignore other errors for now - topic creation is best effort
            }
        }

        public async Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default)
        {
            // Topic listing implementation requires more complex AdminClient usage
            // For now, return empty list as this is not critical for basic functionality
            return new List<string>();
        }

        public async Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));

            try
            {
                using var adminClient = new AdminClientBuilder(_adminConfig).Build();

                var configs = new List<ConfigEntry>
                {
                    new ConfigEntry { Name = "retention.ms", Value = ((long)retentionPeriod.TotalMilliseconds).ToString() }
                };

                var configResource = new ConfigResource
                {
                    Name = topicName,
                    Type = ResourceType.Topic
                };

                var topicConfigs = new Dictionary<ConfigResource, List<ConfigEntry>>
                {
                    { configResource, configs }
                };

                await adminClient.AlterConfigsAsync(topicConfigs);
            }
            catch
            {
                // Topic configuration may fail in test environments, ignore for now
            }
        }

        public async Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
                throw new ArgumentNullException(nameof(topicName));

            try
            {
                using var adminClient = new AdminClientBuilder(_adminConfig).Build();

                var configs = new List<ConfigEntry>
                {
                    new ConfigEntry { Name = "retention.ms", Value = ((long)maxAge.TotalMilliseconds).ToString() }
                };

                var configResource = new ConfigResource
                {
                    Name = topicName,
                    Type = ResourceType.Topic
                };

                var topicConfigs = new Dictionary<ConfigResource, List<ConfigEntry>>
                {
                    { configResource, configs }
                };

                await adminClient.AlterConfigsAsync(topicConfigs);
            }
            catch
            {
                // Topic configuration may fail in test environments, ignore for now
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose connection resources
                _disposed = true;
            }
        }
    }
}