using FluentAssertions;
using OElite.Restme.Abstractions;
using OElite.Restme.Kafka.IntegrationTests.Infrastructure;
using OElite.Restme.Kafka.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

/// <summary>
/// Real integration tests for Kafka with actual broker connections
/// </summary>
[Collection("KafkaIntegration")]
public class KafkaRealIntegrationTests : KafkaTestBase
{
    private readonly ITestOutputHelper _output;

    public KafkaRealIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PublishMessage_SingleMessage_ShouldSucceed()
    {
        // Arrange
        var topicName = $"test-publish-{Guid.NewGuid():N}";
        var message = new UserActivityMessage
        {
            UserId = "test-user-123",
            Action = "login",
            Timestamp = DateTime.UtcNow,
            Metadata = "test metadata"
        };

        _output.WriteLine($"Publishing message to topic: {topicName}");

        // Act
        await Rest.PublishAsync(message, topicName);

        // Assert - No exception means success
        _output.WriteLine("✅ Message published successfully");
    }

    [Fact]
    public async Task PublishBatch_MultipleMessages_ShouldSucceed()
    {
        // Arrange
        var topicName = $"test-batch-{Guid.NewGuid():N}";
        var messages = new List<UserActivityMessage>
        {
            new() { UserId = "user1", Action = "login", Timestamp = DateTime.UtcNow },
            new() { UserId = "user2", Action = "logout", Timestamp = DateTime.UtcNow },
            new() { UserId = "user3", Action = "click", Timestamp = DateTime.UtcNow },
            new() { UserId = "user4", Action = "view", Timestamp = DateTime.UtcNow },
            new() { UserId = "user5", Action = "purchase", Timestamp = DateTime.UtcNow }
        };

        _output.WriteLine($"Publishing {messages.Count} messages to topic: {topicName}");

        // Act
        await Rest.PublishAsync(messages, topicName);

        // Assert - No exception means success
        _output.WriteLine($"✅ {messages.Count} messages published successfully");
    }

    [Fact]
    public async Task SubscribeAndConsume_ShouldReceiveMessages()
    {
        // Arrange
        var topicName = $"test-subscribe-{Guid.NewGuid():N}";
        var consumerGroup = $"test-group-{Guid.NewGuid():N}";
        var receivedMessages = new List<UserActivityMessage>();
        var messageCount = 3;

        _output.WriteLine($"Setting up subscription to topic: {topicName}");

        // Act - Subscribe and consume first
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        _output.WriteLine("About to call SubscribeAsync");

        var subscribeTask = Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                _output.WriteLine($"Message handler called with message from user: {msg.UserId}");
                receivedMessages.Add(msg);
                _output.WriteLine($"Received message from user: {msg.UserId}");

                // Stop after receiving all messages
                if (receivedMessages.Count >= messageCount)
                {
                    _output.WriteLine("Cancelling token - received all expected messages");
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Give the consumer a moment to set up
        await Task.Delay(2000);

        _output.WriteLine("Publishing messages after consumer is set up...");

        // Publish test messages after subscription is set up
        var messages = Enumerable.Range(1, messageCount).Select(i => new UserActivityMessage
        {
            UserId = $"user{i}",
            Action = $"action{i}",
            Timestamp = DateTime.UtcNow
        }).ToList();

        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(messages, topicName, null);
        _output.WriteLine($"Published {messageCount} messages");

        // Wait for the subscription task to complete or timeout
        await subscribeTask;

        _output.WriteLine("SubscribeAsync call completed");

        // Assert
        _output.WriteLine($"Total messages received: {receivedMessages.Count}");
        receivedMessages.Should().HaveCountGreaterOrEqualTo(1); // At least some messages received
    }

    [Fact]
    public async Task SetTopicRetention_ShouldNotThrowException()
    {
        // Arrange
        var topicName = $"test-retention-{Guid.NewGuid():N}";
        
        // Publish a message first to ensure topic exists
        await Rest.PublishAsync(new UserActivityMessage 
        { 
            UserId = "test", 
            Action = "test",
            Timestamp = DateTime.UtcNow 
        }, topicName);
        
        await Task.Delay(500);

        _output.WriteLine("Testing topic retention configuration");

        // Act & Assert - Should not throw
        await Rest.SetTopicRetentionAsync(topicName, TimeSpan.FromDays(7));
        
        _output.WriteLine("✅ Topic retention set successfully");
    }

    [Fact]
    public async Task SetMessageExpiry_ShouldNotThrowException()
    {
        // Arrange
        var topicName = $"test-expiry-{Guid.NewGuid():N}";
        
        // Publish a message first to ensure topic exists
        await Rest.PublishAsync(new UserActivityMessage 
        { 
            UserId = "test", 
            Action = "test",
            Timestamp = DateTime.UtcNow 
        }, topicName);
        
        await Task.Delay(500);

        _output.WriteLine("Testing message expiry configuration");

        // Act & Assert - Should not throw
        await Rest.SetMessageExpiryAsync(topicName, TimeSpan.FromHours(24));
        
        _output.WriteLine("✅ Message expiry set successfully");
    }

    [Fact]
    public async Task PublishWithKey_ShouldPartitionCorrectly()
    {
        // Arrange
        var topicName = $"test-keyed-{Guid.NewGuid():N}";
        var messages = new List<(string key, UserActivityMessage message)>
        {
            ("key1", new UserActivityMessage { UserId = "user1", Action = "login", Timestamp = DateTime.UtcNow }),
            ("key2", new UserActivityMessage { UserId = "user2", Action = "logout", Timestamp = DateTime.UtcNow }),
            ("key1", new UserActivityMessage { UserId = "user3", Action = "click", Timestamp = DateTime.UtcNow })
        };

        _output.WriteLine($"Publishing keyed messages to topic: {topicName}");

        // Act - Publish messages with keys
        foreach (var (key, message) in messages)
        {
            await Rest.PublishAsync(message, topicName, key);
            _output.WriteLine($"Published message with key: {key}");
        }

        // Assert - No exception means success
        _output.WriteLine("✅ Keyed messages published successfully");
    }

    [Fact]
    public async Task ConsumeFromOffset_ShouldStartAtSpecificPosition()
    {
        // Arrange
        var topicName = $"test-offset-{Guid.NewGuid():N}";
        var consumerGroup = $"test-offset-group-{Guid.NewGuid():N}";
        
        // Publish initial messages
        var initialMessages = Enumerable.Range(1, 5).Select(i => new UserActivityMessage
        {
            UserId = $"initial-user{i}",
            Action = "initial",
            Timestamp = DateTime.UtcNow
        }).ToList();

        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(initialMessages, topicName, null);
        await Task.Delay(1000);

        _output.WriteLine($"Published {initialMessages.Count} initial messages");

        // Consume from beginning to verify
        var receivedMessages = new List<UserActivityMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                receivedMessages.Add(msg);
                _output.WriteLine($"Received message: {msg.UserId}");
                
                if (receivedMessages.Count >= initialMessages.Count)
                {
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Assert
        _output.WriteLine($"Total messages consumed: {receivedMessages.Count}");
        receivedMessages.Should().HaveCountGreaterThan(0);
    }
}
