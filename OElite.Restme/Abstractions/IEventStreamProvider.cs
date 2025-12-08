using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Interface for event streaming operations (Kafka, Pulsar, etc.)
/// Provides distributed event log with replay capability, partitioning, and consumer groups.
/// Event streams differ from traditional message queues by persisting events for replayability and multi-subscriber consumption.
/// </summary>
/// <remarks>
/// Event Streaming vs Message Queue:
/// - Event Stream: Persistent log, multiple consumers, replay capability, ordered by partition
/// - Message Queue: Consumed once, single consumer per message, no replay
/// 
/// Typical use cases:
/// - Event sourcing and CQRS patterns
/// - Change data capture (CDC)
/// - Real-time analytics and monitoring
/// - Microservices event-driven architectures
/// </remarks>
public interface IEventStreamProvider : IRestmeProvider
{
    /// <summary>
    /// Publish a single event to a topic
    /// </summary>
    /// <param name="message">The event message to publish</param>
    /// <param name="topicName">The topic name to publish to</param>
    /// <param name="key">Optional partition key for ordering (messages with same key go to same partition)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T message, string topicName, string key = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish multiple events to a topic in batch for improved performance
    /// </summary>
    /// <param name="messages">The collection of event messages to publish</param>
    /// <param name="topicName">The topic name to publish to</param>
    /// <param name="keySelector">Optional function to extract partition key from each message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to events from a topic with a consumer group
    /// Multiple consumers in the same group share the load (partitions distributed among consumers)
    /// </summary>
    /// <param name="topicName">The topic name to subscribe to</param>
    /// <param name="consumerGroup">Consumer group name for load balancing</param>
    /// <param name="messageHandler">Handler function to process each event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to multiple topics matching a pattern with a consumer group
    /// </summary>
    /// <param name="topicPattern">Regex pattern to match topic names</param>
    /// <param name="consumerGroup">Consumer group name for load balancing</param>
    /// <param name="messageHandler">Handler function receiving topic name and event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process events from a topic and return a stream result
    /// </summary>
    /// <param name="topicName">The topic name to process</param>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a topic with specified partitioning and replication configuration
    /// </summary>
    /// <param name="topicName">The topic name to create</param>
    /// <param name="partitions">Number of partitions (default: 1)</param>
    /// <param name="replicationFactor">Replication factor for fault tolerance (default: 1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available topics in the event streaming platform
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Set retention policy for a topic (how long events are kept)
    /// </summary>
    /// <param name="topicName">The topic name</param>
    /// <param name="retentionPeriod">Time period to retain events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure message timestamp-based expiry for a topic
    /// </summary>
    /// <param name="topicName">The topic name</param>
    /// <param name="maxAge">Maximum age for events before expiration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default);
}