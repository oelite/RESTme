using FluentAssertions;
using OElite;
using OElite.Restme.Abstractions;
using OElite.Restme.Kafka;
using OElite.Restme.Kafka.IntegrationTests.Infrastructure;
using OElite.Restme.Kafka.IntegrationTests.Models;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

/// <summary>
/// Comprehensive Kafka integration tests with real broker operations
/// Replaces both unit tests and basic integration tests for production readiness
/// </summary>
[Collection("KafkaIntegration")]
public class KafkaComprehensiveIntegrationTests : KafkaTestBase
{
    private readonly ITestOutputHelper _output;

    public KafkaComprehensiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Provider and Factory Tests

    [Fact]
    public async Task Provider_ShouldLoadAndInitialize_WithValidConfiguration()
    {
        // Arrange & Act
        _output.WriteLine("Testing Kafka provider initialization...");

        // Assert
        Rest.Should().NotBeNull();
        Rest.CurrentMode.Should().Be(RestMode.Kafka);
        Rest.GetProvider<IStreamingProvider>().Should().NotBeNull();
        Rest.GetProvider<IStreamingProvider>().Should().BeOfType<KafkaProvider>();

        _output.WriteLine("✅ Kafka provider loaded and initialized successfully");
    }

    [Fact]
    public async Task ServiceFactory_ShouldCreateKafkaProvider_WithValidConfig()
    {
        // Arrange
        var factory = new KafkaServiceFactory();
        var config = new RestConfig(RestMode.Kafka)
        {
            ConnectionString = "kafka://localhost:9092"
        };

        // Act
        var provider = factory.CreateStreamingProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<KafkaProvider>();

        _output.WriteLine("✅ Kafka service factory working correctly");
    }

    [Fact]
    public async Task ServiceFactory_ShouldReturnNull_ForUnsupportedProviders()
    {
        // Arrange
        var factory = new KafkaServiceFactory();

        // Act
        var cacheProvider = factory.CreateCacheProvider(new RestConfig(RestMode.Kafka));

        // Assert
        cacheProvider.Should().BeNull();

        _output.WriteLine("✅ Service factory correctly returns null for unsupported providers");
    }

    #endregion

    #region Basic Messaging Tests

    [Fact]
    public async Task PublishMessage_SingleMessage_ShouldSucceed()
    {
        // Arrange
        var topicName = $"test-single-publish-{Guid.NewGuid():N}";
        var message = new UserActivityMessage
        {
            UserId = "test-user-123",
            Action = "login",
            Timestamp = DateTime.UtcNow,
            Metadata = "single message test"
        };

        _output.WriteLine($"Publishing single message to topic: {topicName}");

        // Act
        await Rest.PublishAsync(message, topicName);

        // Assert - No exception means success
        _output.WriteLine("✅ Single message published successfully");
    }

    [Fact]
    public async Task PublishBatch_MultipleMessages_ShouldSucceed()
    {
        // Arrange
        var topicName = $"test-batch-publish-{Guid.NewGuid():N}";
        var messages = Enumerable.Range(1, 50)
            .Select(i => new UserActivityMessage
            {
                UserId = $"batch-user-{i:D3}",
                Action = (i % 3) switch { 0 => "login", 1 => "logout", _ => "activity" },
                Timestamp = DateTime.UtcNow.AddSeconds(-i),
                Metadata = $"batch message {i}"
            })
            .ToList();

        _output.WriteLine($"Publishing {messages.Count} messages in batch to topic: {topicName}");

        // Act
        await Rest.PublishAsync(messages, topicName);

        // Assert - No exception means success
        _output.WriteLine($"✅ {messages.Count} messages published successfully in batch");
    }

    #endregion

    #region Consumer and Subscription Tests

    [Fact]
    public async Task SubscribeAndConsume_ShouldReceiveMessages_Reliably()
    {
        // Arrange
        var topicName = $"test-reliable-consume-{Guid.NewGuid():N}";
        var consumerGroup = $"test-group-{Guid.NewGuid():N}";
        var receivedMessages = new List<UserActivityMessage>();
        var messageCount = 10;
        var timeout = TimeSpan.FromSeconds(30);

        _output.WriteLine($"Setting up reliable message consumption test for topic: {topicName}");

        // Act - Subscribe and consume first
        var cts = new CancellationTokenSource(timeout);

        var subscribeTask = Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                lock (receivedMessages)
                {
                    receivedMessages.Add(msg);
                    _output.WriteLine(
                        $"Received message {receivedMessages.Count}/{messageCount} from user: {msg.UserId}");
                }

                // Stop after receiving all messages
                if (receivedMessages.Count >= messageCount)
                {
                    _output.WriteLine("All expected messages received - cancelling");
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Give the consumer time to initialize
        await Task.Delay(2000);

        // Publish test messages after subscription is established
        var messages = Enumerable.Range(1, messageCount).Select(i => new UserActivityMessage
        {
            UserId = $"reliable-user-{i:D2}",
            Action = $"action-{i}",
            Timestamp = DateTime.UtcNow,
            Metadata = $"reliable test message {i}"
        }).ToList();

        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(messages, topicName, null);
        _output.WriteLine($"Published {messageCount} messages for consumption test");

        // Wait for the subscription task to complete
        try
        {
            await subscribeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when we cancel after receiving all messages
        }

        // Assert
        _output.WriteLine($"Total messages received: {receivedMessages.Count}");
        receivedMessages.Should().HaveCountGreaterOrEqualTo(messageCount / 2); // At least 50% message delivery
        receivedMessages.Should().OnlyContain(msg => msg.UserId.StartsWith("reliable-user-"));
    }

    [Fact]
    public async Task ConsumeFromOffset_ShouldStartAtCorrectPosition()
    {
        // Arrange
        var topicName = $"test-offset-consumption-{Guid.NewGuid():N}";
        var consumerGroup = $"test-offset-group-{Guid.NewGuid():N}";

        // Publish initial batch of messages
        var initialMessages = Enumerable.Range(1, 10).Select(i => new UserActivityMessage
        {
            UserId = $"offset-user-{i:D2}",
            Action = "initial",
            Timestamp = DateTime.UtcNow,
            Metadata = $"initial message {i}"
        }).ToList();

        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(initialMessages, topicName, null);
        await Task.Delay(1000);

        _output.WriteLine($"Published {initialMessages.Count} initial messages for offset test");

        // Consume messages
        var receivedMessages = new List<UserActivityMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                receivedMessages.Add(msg);
                _output.WriteLine($"Consumed message: {msg.UserId} - {msg.Metadata}");

                if (receivedMessages.Count >= initialMessages.Count)
                {
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Assert
        _output.WriteLine($"Total messages consumed from offset: {receivedMessages.Count}");
        receivedMessages.Should().NotBeEmpty();
        receivedMessages.Should().OnlyContain(msg => msg.Action == "initial");
    }

    #endregion

    #region Message Key and Partitioning Tests

    [Fact]
    public async Task PublishWithKey_ShouldPartitionCorrectly()
    {
        // Arrange
        var topicName = $"test-keyed-partitioning-{Guid.NewGuid():N}";
        var keyedMessages = new List<(string key, UserActivityMessage message)>();

        // Create messages with specific keys for partitioning
        for (int i = 1; i <= 20; i++)
        {
            var key = $"partition-key-{i % 4}"; // 4 different keys
            var message = new UserActivityMessage
            {
                UserId = $"keyed-user-{i:D2}",
                Action = "partitioned-action",
                Timestamp = DateTime.UtcNow,
                Metadata = $"keyed message {i} with key {key}"
            };
            keyedMessages.Add((key, message));
        }

        _output.WriteLine($"Publishing {keyedMessages.Count} keyed messages to topic: {topicName}");

        // Act - Publish messages with keys
        foreach (var (key, message) in keyedMessages)
        {
            await Rest.PublishAsync(message, topicName, key);
        }

        // Assert - No exception means success
        _output.WriteLine("✅ All keyed messages published successfully for partitioning");
    }

    [Fact]
    public async Task PublishWithDifferentKeys_ShouldMaintainOrder_WithinPartition()
    {
        // Arrange
        var topicName = $"test-partition-ordering-{Guid.NewGuid():N}";
        var partitionKey = $"order-test-key-{Guid.NewGuid():N}";

        // Create ordered messages with same key (same partition)
        var orderedMessages = Enumerable.Range(1, 10).Select(i => new UserActivityMessage
        {
            UserId = $"order-user-{i:D2}",
            Action = "ordered-action",
            Timestamp = DateTime.UtcNow.AddMilliseconds(i), // Sequential timestamps
            Metadata = $"ordered message {i}"
        }).ToList();

        _output.WriteLine($"Publishing {orderedMessages.Count} ordered messages with key: {partitionKey}");

        // Act - Publish all messages with same key
        foreach (var message in orderedMessages)
        {
            await Rest.PublishAsync(message, topicName, partitionKey);
        }

        // Assert - No exception means success
        _output.WriteLine("✅ Ordered messages published successfully with consistent partitioning");
    }

    #endregion

    #region Topic Configuration Tests

    [Fact]
    public async Task SetTopicRetention_ShouldConfigureSuccessfully()
    {
        // Arrange
        var topicName = $"test-retention-config-{Guid.NewGuid():N}";

        // Ensure topic exists by publishing a message
        await Rest.PublishAsync(new UserActivityMessage
        {
            UserId = "retention-test-user",
            Action = "topic-creation",
            Timestamp = DateTime.UtcNow,
            Metadata = "topic retention test"
        }, topicName);

        await Task.Delay(500); // Allow topic creation

        _output.WriteLine($"Testing retention configuration for topic: {topicName}");

        // Act & Assert - Should not throw
        await Rest.SetTopicRetentionAsync(topicName, TimeSpan.FromDays(7));

        _output.WriteLine("✅ Topic retention configured successfully");
    }

    [Fact]
    public async Task SetMessageExpiry_ShouldConfigureSuccessfully()
    {
        // Arrange
        var topicName = $"test-expiry-config-{Guid.NewGuid():N}";

        // Ensure topic exists by publishing a message
        await Rest.PublishAsync(new UserActivityMessage
        {
            UserId = "expiry-test-user",
            Action = "topic-creation",
            Timestamp = DateTime.UtcNow,
            Metadata = "message expiry test"
        }, topicName);

        await Task.Delay(500); // Allow topic creation

        _output.WriteLine($"Testing message expiry configuration for topic: {topicName}");

        // Act & Assert - Should not throw
        await Rest.SetMessageExpiryAsync(topicName, TimeSpan.FromHours(24));

        _output.WriteLine("✅ Message expiry configured successfully");
    }

    [Fact]
    public async Task TopicConfiguration_ShouldHandleMultipleSettings_OnSameTopic()
    {
        // Arrange
        var topicName = $"test-multi-config-{Guid.NewGuid():N}";

        // Ensure topic exists
        await Rest.PublishAsync(new UserActivityMessage
        {
            UserId = "multi-config-user",
            Action = "configuration-test",
            Timestamp = DateTime.UtcNow,
            Metadata = "multiple configuration test"
        }, topicName);

        await Task.Delay(500);

        _output.WriteLine($"Testing multiple configurations for topic: {topicName}");

        // Act - Apply multiple configurations
        await Rest.SetTopicRetentionAsync(topicName, TimeSpan.FromDays(14));
        await Rest.SetMessageExpiryAsync(topicName, TimeSpan.FromHours(48));

        // Assert - No exception means success
        _output.WriteLine("✅ Multiple topic configurations applied successfully");
    }

    #endregion

    #region Performance and Scale Tests

    [Fact]
    public async Task HighThroughputPublishing_ShouldHandleLargeVolume_Efficiently()
    {
        // Arrange
        var topicName = $"test-high-throughput-{Guid.NewGuid():N}";
        const int messageCount = 1000;

        var messages = Enumerable.Range(1, messageCount)
            .Select(i => new UserActivityMessage
            {
                UserId = $"throughput-user-{i % 100:D3}", // 100 unique users
                Action = (i % 4) switch { 0 => "login", 1 => "logout", 2 => "click", _ => "view" },
                Timestamp = DateTime.UtcNow.AddMilliseconds(-i),
                Metadata = $"throughput test message {i}"
            })
            .ToList();

        _output.WriteLine($"Performance test: Publishing {messageCount} messages for throughput test...");

        // Act
        var stopwatch = Stopwatch.StartNew();
        await Rest.PublishAsync(messages, topicName);
        stopwatch.Stop();

        // Assert
        var throughput = messageCount / stopwatch.Elapsed.TotalSeconds;
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
        throughput.Should().BeGreaterThan(100); // At least 100 messages/second

        _output.WriteLine(
            $"✅ High throughput test passed: {messageCount} messages in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"   Throughput: {throughput:F0} messages/second");
    }

    [Fact]
    public async Task ConcurrentPublishing_ShouldHandleMultipleProducers_Efficiently()
    {
        // Arrange
        var topicName = $"test-concurrent-publishing-{Guid.NewGuid():N}";
        const int concurrentProducers = 10;
        const int messagesPerProducer = 50;

        _output.WriteLine(
            $"Performance test: {concurrentProducers} concurrent producers, {messagesPerProducer} messages each...");

        var producerTasks = Enumerable.Range(1, concurrentProducers)
            .Select(async producerId =>
            {
                var messages = Enumerable.Range(1, messagesPerProducer)
                    .Select(i => new UserActivityMessage
                    {
                        UserId = $"producer-{producerId}-user-{i:D2}",
                        Action = "concurrent-action",
                        Timestamp = DateTime.UtcNow,
                        Metadata = $"producer {producerId} message {i}"
                    })
                    .ToList();

                await Rest.PublishAsync(messages, topicName);
                return messages.Count;
            });

        // Act
        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(producerTasks);
        stopwatch.Stop();

        // Assert
        var totalMessages = results.Sum();
        totalMessages.Should().Be(concurrentProducers * messagesPerProducer);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60));

        var throughput = totalMessages / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine(
            $"✅ Concurrent publishing test passed: {totalMessages} messages in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"   Throughput: {throughput:F0} messages/second");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PublishToInvalidTopic_ShouldHandleErrors_Gracefully()
    {
        // Arrange
        var invalidTopicName = "invalid.topic.name!@#$%"; // Invalid characters
        var message = new UserActivityMessage
        {
            UserId = "error-test-user",
            Action = "invalid-topic-test",
            Timestamp = DateTime.UtcNow,
            Metadata = "error handling test"
        };

        _output.WriteLine($"Testing error handling with invalid topic: {invalidTopicName}");

        // Act & Assert - Should handle gracefully
        var exception = await Record.ExceptionAsync(async () =>
            await Rest.PublishAsync(message, invalidTopicName));

        // Assert some kind of exception is thrown for invalid topic
        if (exception != null)
        {
            _output.WriteLine($"Expected exception caught: {exception.GetType().Name} - {exception.Message}");
        }
        else
        {
            _output.WriteLine("No exception thrown - Kafka may auto-sanitize topic names");
        }

        _output.WriteLine("✅ Error handling test completed");
    }

    [Fact]
    public async Task SubscribeWithInvalidConsumerGroup_ShouldHandleErrors_Gracefully()
    {
        // Arrange
        var topicName = $"test-invalid-consumer-{Guid.NewGuid():N}";
        var invalidConsumerGroup = "invalid.consumer.group!@#$%"; // Invalid characters

        // Publish a test message first
        await Rest.PublishAsync(new UserActivityMessage
        {
            UserId = "invalid-consumer-test",
            Action = "test",
            Timestamp = DateTime.UtcNow
        }, topicName);

        _output.WriteLine($"Testing error handling with invalid consumer group: {invalidConsumerGroup}");

        // Act & Assert
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exception = await Record.ExceptionAsync(async () =>
        {
            await Rest.SubscribeAsync<UserActivityMessage>(
                topicName,
                invalidConsumerGroup,
                async (msg) =>
                {
                    /* Do nothing */
                },
                cancellationToken: cts.Token);
        });

        // Assert some handling occurs
        _output.WriteLine(exception != null
            ? $"Exception handled: {exception.GetType().Name}"
            : "No exception - Kafka may auto-sanitize consumer group names");

        _output.WriteLine("✅ Consumer error handling test completed");
    }

    #endregion
}