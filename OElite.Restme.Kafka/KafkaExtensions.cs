using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite
{
    /// <summary>
    /// Kafka extension methods for IRestme
    /// </summary>
    public static class KafkaRestmeExtensions
    {
        /// <summary>
        /// Publish a single message to a Kafka topic
        /// </summary>
        public static async Task PublishAsync<T>(this IRestme rest, T message,
            string topicName, string key = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.PublishAsync(message, topicName, key, cancellationToken);
        }

        /// <summary>
        /// Publish multiple messages to a Kafka topic in batch
        /// </summary>
        public static async Task PublishAsync<T>(this IRestme rest, IEnumerable<T> messages,
            string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.PublishBatchAsync(messages, topicName, keySelector, cancellationToken);
        }

        /// <summary>
        /// Subscribe to a Kafka topic with a message handler
        /// </summary>
        public static async Task SubscribeAsync<T>(this IRestme rest, string topicName,
            string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.SubscribeAsync(topicName, consumerGroup, messageHandler, cancellationToken);
        }

        /// <summary>
        /// Subscribe to topics matching a pattern with a message handler
        /// </summary>
        public static async Task SubscribePatternAsync<T>(this IRestme rest, string topicPattern,
            string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.SubscribePatternAsync(topicPattern, consumerGroup, messageHandler, cancellationToken);
        }

        /// <summary>
        /// List all available Kafka topics
        /// </summary>
        public static async Task<List<string>> ListTopicsAsync(this IRestme rest,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            return await rest.StreamingProvider.ListTopicsAsync(cancellationToken);
        }

        /// <summary>
        /// Set retention policy for a Kafka topic (message expiry)
        /// </summary>
        public static async Task SetTopicRetentionAsync(this IRestme rest, string topicName,
            TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.SetTopicRetentionAsync(topicName, retentionPeriod, cancellationToken);
        }

        /// <summary>
        /// Configure message timestamp-based expiry for a topic
        /// </summary>
        public static async Task SetMessageExpiryAsync(this IRestme rest, string topicName,
            TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.SetMessageExpiryAsync(topicName, maxAge, cancellationToken);
        }

        /// <summary>
        /// Create a Kafka topic with specified configuration
        /// </summary>
        public static async Task CreateTopicAsync(this IRestme rest, string topicName,
            int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.Kafka)
                throw new OEliteException("Kafka mode required");

            if (rest.StreamingProvider == null)
                throw new OEliteException("Kafka provider not initialized");

            await rest.StreamingProvider.CreateTopicAsync(topicName, partitions, replicationFactor, cancellationToken);
        }


    }
}