using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite.Restme.Kafka
{
    /// <summary>
    /// Kafka streaming provider implementation
    /// Provides simplified access to Kafka topics, partitions, and consumer groups
    /// </summary>
    public class KafkaProvider : IStreamingProvider
    {
        private readonly KafkaConnection _connection;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public KafkaProvider(string connectionString, RestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Check if Confluent.Kafka library is available
            try
            {
                var testConfig = new Confluent.Kafka.ProducerConfig();
            }
            catch
            {
                throw new NotImplementedException("Confluent.Kafka library is not available. Please install the Confluent.Kafka NuGet package.");
            }

            _connection = new KafkaConnection(connectionString);
        }

        public async Task PublishAsync<T>(T message, string topicName, string key = null, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));

            await _connection.PublishAsync(message, topicName, key, cancellationToken);
        }

        public async Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));

            await _connection.PublishBatchAsync(messages, topicName, keySelector, cancellationToken);
        }

        public async Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));
            if (string.IsNullOrEmpty(consumerGroup)) throw new ArgumentNullException(nameof(consumerGroup));
            if (messageHandler == null) throw new ArgumentNullException(nameof(messageHandler));

            await _connection.SubscribeAsync(topicName, consumerGroup, messageHandler, cancellationToken);
        }

        public async Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicPattern)) throw new ArgumentNullException(nameof(topicPattern));
            if (string.IsNullOrEmpty(consumerGroup)) throw new ArgumentNullException(nameof(consumerGroup));
            if (messageHandler == null) throw new ArgumentNullException(nameof(messageHandler));

            await _connection.SubscribePatternAsync(topicPattern, consumerGroup, messageHandler, cancellationToken);
        }

        public async Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));
            if (string.IsNullOrEmpty(consumerGroup)) throw new ArgumentNullException(nameof(consumerGroup));

            return await _connection.ProcessAsync<T>(topicName, consumerGroup, cancellationToken);
        }

        public async Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));

            await _connection.CreateTopicAsync(topicName, partitions, replicationFactor, cancellationToken);
        }

        public async Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default)
        {
            return await _connection.ListTopicsAsync(cancellationToken);
        }

        public async Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));
            await _connection.SetTopicRetentionAsync(topicName, retentionPeriod, cancellationToken);
        }

        public async Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName)) throw new ArgumentNullException(nameof(topicName));
            await _connection.SetMessageExpiryAsync(topicName, maxAge, cancellationToken);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}