using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Interface for Kafka streaming operations
/// Provides simplified access to Kafka topics, partitions, and consumer groups
/// </summary>
public interface IStreamingProvider : IRestmeProvider
{
    /// <summary>
    /// Publish a single message to a Kafka topic
    /// </summary>
    Task PublishAsync<T>(T message, string topicName, string key = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple messages to a Kafka topic in batch
    /// </summary>
    Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to a Kafka topic with a message handler
    /// </summary>
    Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to topics matching a pattern with a message handler
    /// </summary>
    Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process messages from a topic and return a stream result
    /// </summary>
    Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Kafka topic with specified configuration
    /// </summary>
    Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available Kafka topics
    /// </summary>
    Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Set retention policy for a Kafka topic (message expiry)
    /// </summary>
    Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure message timestamp-based expiry for a topic
    /// </summary>
    Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default);
}