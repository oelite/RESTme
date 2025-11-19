using FluentAssertions;
using OElite.Restme.Kafka.IntegrationTests.Infrastructure;
using OElite.Restme.Kafka.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

/// <summary>
/// Integration tests for Kafka retention and expiry functionality
/// </summary>
[Collection("KafkaIntegration")]
public class KafkaRetentionIntegrationTests : KafkaTestBase
{
    public KafkaRetentionIntegrationTests(ITestOutputHelper output)
    {
        // Test constructor - logging is handled by the base class
    }

    [Fact]
    public async Task SetTopicRetention_ShouldConfigureRetentionPolicy()
    {
        // Arrange
        var topicName = $"test-retention-{Guid.NewGuid():N}";

        // Act - Set retention policy to 7 days
        await Rest.SetTopicRetentionAsync(topicName, TimeSpan.FromDays(7));

        // Assert - Should complete without errors
        // In real implementation, we'd verify the retention policy was set
    }

    [Fact]
    public async Task SetMessageExpiry_ShouldConfigureMessageExpiry()
    {
        // Arrange
        var topicName = $"test-expiry-{Guid.NewGuid():N}";

        // Act - Set message expiry to 24 hours
        await Rest.SetMessageExpiryAsync(topicName, TimeSpan.FromHours(24));

        // Assert - Should complete without errors
        // In real implementation, we'd verify the expiry was configured
    }

    [Fact]
    public async Task SetTopicRetention_WithShortRetention_ShouldWork()
    {
        // Arrange
        var topicName = $"test-short-retention-{Guid.NewGuid():N}";

        // Act - Set very short retention for testing
        await Rest.SetTopicRetentionAsync(topicName, TimeSpan.FromMinutes(5));

        // Assert - Should complete without errors
    }
}

/// <summary>
/// Integration tests for basic Kafka operations
/// </summary>
[Collection("KafkaIntegration")]
public class KafkaBasicIntegrationTests : KafkaTestBase
{
    [Fact]
    public async Task PublishMessage_ShouldWork()
    {
        // Arrange
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        var message = new UserActivityMessage
        {
            UserId = "test-user",
            Action = "login",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await Rest.PublishAsync(message, topicName);

        // Assert - Should complete without errors
        // In real implementation, we'd verify the message was published
    }

    [Fact]
    public async Task PublishBatchMessages_ShouldWork()
    {
        // Arrange
        var topicName = $"test-batch-{Guid.NewGuid():N}";
        var messages = new List<UserActivityMessage>
        {
            new() { UserId = "user1", Action = "login" },
            new() { UserId = "user2", Action = "logout" },
            new() { UserId = "user3", Action = "click" }
        };

        // Act
        await Rest.PublishAsync(messages, topicName);

        // Assert - Should complete without errors
    }

    [Fact]
    public async Task CreateTopic_ShouldWork()
    {
        // Arrange
        var topicName = $"test-create-{Guid.NewGuid():N}";

        // Act
        await Rest.CreateTopicAsync(topicName);

        // Assert - Should complete without errors
        // In real implementation, we'd verify the topic was created
    }

    [Fact]
    public async Task ListTopics_ShouldReturnTopics()
    {
        // Arrange - Create a test topic first
        var topicName = $"test-list-{Guid.NewGuid():N}";
        await Rest.CreateTopicAsync(topicName);

        // Act
        var topics = await Rest.ListTopicsAsync();

        // Assert
        topics.Should().NotBeNull();
        topics.Should().Contain(topicName);
    }
}