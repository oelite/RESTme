using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OElite;
using OElite.Restme;
using OElite.Restme.Abstractions;
using OElite.Restme.RabbitMQ.IntegrationTests.Infrastructure;
using OElite.Restme.RabbitMQ.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.RabbitMQ.IntegrationTests.Tests;

/// <summary>
/// Advanced enterprise-grade integration tests for RabbitMQ provider
/// Focuses on edge cases, resilience patterns, and enterprise scenarios
/// </summary>
[Collection("RabbitMQIntegration")]
public class RabbitMQAdvancedIntegrationTests : RabbitMQTestBase
{
    private readonly ITestOutputHelper _output;

    public RabbitMQAdvancedIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Enterprise Resilience Tests

    [Fact]
    public async Task EnterpriseResilience_MessageDurability_ShouldPersistAcrossConnections()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("durability-test");
        var testMessage = new QueueMessage
        {
            Content = "Durable message test",
            Source = "DurabilityTest"
        };

        _output.WriteLine($"Testing message durability on queue: {queueName}");

        // Act - Publish with durability
        await QueueProvider.PublishAsync(testMessage, queueName,
            isDurable: true, isMessagePersistent: true, autoDelete: false);

        _output.WriteLine("✅ Durable message published successfully");

        // Simulate disconnect/reconnect by creating new provider instance
        var newRest = new Rest(ConnectionString, new RestConfig
        {
            OperationMode = RestMode.RabbitMq,
            AuthKey = "testuser",
            AuthSecret = "testpass"
        });

        var newProvider = newRest.GetProvider<IQueueProvider>();
        newProvider.Should().NotBeNull();

        var receivedMessage = default(QueueMessage);
        var messageReceived = new TaskCompletionSource<bool>();

        // Start consumer with new provider
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumerTask = newProvider!.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                receivedMessage = msg;
                messageReceived.TrySetResult(true);
                cts.Cancel();
                return true;
            },
            queueName: queueName,
            isDurable: true,
            autoDelete: false,
            cancellationToken: cts.Token);

        // Assert - Message should survive connection change
        await messageReceived.Task;
        await consumerTask;

        receivedMessage.Should().NotBeNull();
        receivedMessage!.Content.Should().Be(testMessage.Content);
        receivedMessage.Id.Should().Be(testMessage.Id);

        _output.WriteLine("✅ Message durability validated across connection changes");

        // Cleanup
        if (newProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task EnterpriseResilience_ConcurrentPublishers_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("concurrent-publishers");
        var publisherCount = 5;
        var messagesPerPublisher = 20;
        var totalExpectedMessages = publisherCount * messagesPerPublisher;

        var receivedMessages = new ConcurrentBag<QueueMessage>();
        var allReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing concurrent publishers: {publisherCount} publishers × {messagesPerPublisher} messages");

        // Start consumer first
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                receivedMessages.Add(msg);
                _output.WriteLine($"Received message {receivedMessages.Count}/{totalExpectedMessages}: {msg.Content}");

                if (receivedMessages.Count >= totalExpectedMessages)
                {
                    allReceived.TrySetResult(true);
                    cts.Cancel();
                }
                return true;
            },
            queueName: queueName,
            prefetchCount: 50, // Higher prefetch for concurrent scenario
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act - Create concurrent publishers
        var publisherTasks = Enumerable.Range(0, publisherCount).Select(async publisherId =>
        {
            var tasks = Enumerable.Range(0, messagesPerPublisher).Select(async messageId =>
            {
                var message = new QueueMessage
                {
                    Content = $"Publisher-{publisherId}-Message-{messageId}",
                    Source = $"Publisher{publisherId}",
                    Priority = messageId
                };

                await QueueProvider.PublishAsync(message, queueName);
                await Task.Delay(Random.Shared.Next(1, 10)); // Random delay to simulate real concurrency
            });

            await Task.WhenAll(tasks);
            _output.WriteLine($"✅ Publisher {publisherId} completed all {messagesPerPublisher} messages");
        });

        await Task.WhenAll(publisherTasks);
        _output.WriteLine($"✅ All {publisherCount} concurrent publishers completed");

        // Wait for all messages to be consumed
        await allReceived.Task;
        await consumerTask;

        // Assert - Validate data integrity
        receivedMessages.Should().HaveCount(totalExpectedMessages);

        // Check for duplicates (data integrity violation)
        var messageIds = receivedMessages.Select(m => m.Id).ToList();
        var uniqueIds = messageIds.Distinct().ToList();
        uniqueIds.Should().HaveCount(totalExpectedMessages, "No duplicate messages should exist");

        // Verify each publisher's messages
        for (int publisherId = 0; publisherId < publisherCount; publisherId++)
        {
            var publisherMessages = receivedMessages.Where(m => m.Source == $"Publisher{publisherId}").ToList();
            publisherMessages.Should().HaveCount(messagesPerPublisher,
                $"Publisher {publisherId} should have exactly {messagesPerPublisher} messages");
        }

        _output.WriteLine($"✅ Concurrent publisher data integrity validated: {totalExpectedMessages} unique messages");
    }

    [Fact]
    public async Task EnterpriseResilience_MessageOrdering_ShouldMaintainFIFOWithSingleConsumer()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("ordering-test");
        var messageCount = 50;
        var receivedMessages = new List<QueueMessage>();
        var allReceived = new TaskCompletionSource<bool>();
        var lockObject = new object();

        _output.WriteLine($"Testing FIFO message ordering with {messageCount} messages on queue: {queueName}");

        // Start single consumer to ensure ordering
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                lock (lockObject)
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= messageCount)
                    {
                        allReceived.TrySetResult(true);
                        cts.Cancel();
                    }
                }

                // Small processing delay to test ordering under load
                await Task.Delay(5);
                return true;
            },
            queueName: queueName,
            prefetchCount: 1, // Critical: Single message prefetch to ensure FIFO
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act - Publish messages in sequence
        var publishedMessages = new List<QueueMessage>();
        for (int i = 0; i < messageCount; i++)
        {
            var message = new QueueMessage
            {
                Content = $"Ordered message {i:D3}",
                Priority = i
            };
            publishedMessages.Add(message);
            await QueueProvider.PublishAsync(message, queueName);
        }

        _output.WriteLine($"✅ Published {messageCount} messages in sequence");

        // Wait for all messages to be consumed
        await allReceived.Task;
        await consumerTask;

        // Assert - Validate FIFO ordering
        receivedMessages.Should().HaveCount(messageCount);

        for (int i = 0; i < messageCount; i++)
        {
            receivedMessages[i].Priority.Should().Be(i,
                $"Message at position {i} should have priority {i} (FIFO violation)");
            receivedMessages[i].Content.Should().Be($"Ordered message {i:D3}");
        }

        _output.WriteLine($"✅ FIFO ordering validated: all {messageCount} messages in correct sequence");
    }

    #endregion

    #region Advanced Exchange Patterns

    [Fact]
    public async Task AdvancedExchange_TopicRouting_ShouldRouteByPatterns()
    {
        // Arrange
        var exchangeName = CreateUniqueExchangeName("topic-test");
        var queue1 = CreateUniqueQueueName("orders");
        var queue2 = CreateUniqueQueueName("payments");
        var queue3 = CreateUniqueQueueName("all-events");

        // Setup topic exchange with pattern-based routing
        await QueueProvider.DeclareExchangeAsync(exchangeName, "topic", isDurable: false);
        await QueueProvider.DeclareQueueAsync(queue1, isDurable: false);
        await QueueProvider.DeclareQueueAsync(queue2, isDurable: false);
        await QueueProvider.DeclareQueueAsync(queue3, isDurable: false);

        // Bind with patterns
        await QueueProvider.BindQueueAsync(queue1, exchangeName, "order.*");
        await QueueProvider.BindQueueAsync(queue2, exchangeName, "payment.*");
        await QueueProvider.BindQueueAsync(queue3, exchangeName, "*"); // Catch all

        var orderMessages = new ConcurrentBag<UserActivityMessage>();
        var paymentMessages = new ConcurrentBag<UserActivityMessage>();
        var allMessages = new ConcurrentBag<UserActivityMessage>();
        var allRouted = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing topic exchange routing patterns: {exchangeName}");

        // Start consumers
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var orderConsumer = QueueProvider.StartConsumingAsync<UserActivityMessage>(
            async (msg) =>
            {
                orderMessages.Add(msg);
                _output.WriteLine($"Order queue received: {msg.Action}");
                CheckAllRouted();
                return true;
            },
            queueName: queue1,
            cancellationToken: cts.Token);

        var paymentConsumer = QueueProvider.StartConsumingAsync<UserActivityMessage>(
            async (msg) =>
            {
                paymentMessages.Add(msg);
                _output.WriteLine($"Payment queue received: {msg.Action}");
                CheckAllRouted();
                return true;
            },
            queueName: queue2,
            cancellationToken: cts.Token);

        var allConsumer = QueueProvider.StartConsumingAsync<UserActivityMessage>(
            async (msg) =>
            {
                allMessages.Add(msg);
                _output.WriteLine($"All-events queue received: {msg.Action}");
                CheckAllRouted();
                return true;
            },
            queueName: queue3,
            cancellationToken: cts.Token);

        void CheckAllRouted()
        {
            // Wait for all expected routing combinations
            if (orderMessages.Count >= 2 && paymentMessages.Count >= 2 && allMessages.Count >= 4)
            {
                allRouted.TrySetResult(true);
                cts.Cancel();
            }
        }

        await Task.Delay(1000);

        // Act - Publish with various routing keys
        await QueueProvider.PublishAsync(
            new UserActivityMessage { Action = "order.created" },
            exchangeName: exchangeName, routingKey: "order.created");

        await QueueProvider.PublishAsync(
            new UserActivityMessage { Action = "order.paid" },
            exchangeName: exchangeName, routingKey: "order.paid");

        await QueueProvider.PublishAsync(
            new UserActivityMessage { Action = "payment.processed" },
            exchangeName: exchangeName, routingKey: "payment.processed");

        await QueueProvider.PublishAsync(
            new UserActivityMessage { Action = "payment.failed" },
            exchangeName: exchangeName, routingKey: "payment.failed");

        await allRouted.Task;
        await Task.WhenAll(orderConsumer, paymentConsumer, allConsumer);

        // Assert - Validate topic routing
        orderMessages.Should().HaveCount(2, "Order queue should receive order.* messages");
        paymentMessages.Should().HaveCount(2, "Payment queue should receive payment.* messages");
        allMessages.Should().HaveCount(4, "All-events queue should receive all messages");

        var orderActions = orderMessages.Select(m => m.Action).ToList();
        orderActions.Should().Contain("order.created");
        orderActions.Should().Contain("order.paid");

        var paymentActions = paymentMessages.Select(m => m.Action).ToList();
        paymentActions.Should().Contain("payment.processed");
        paymentActions.Should().Contain("payment.failed");

        _output.WriteLine($"✅ Topic exchange routing validated successfully");
    }

    [Fact]
    public async Task AdvancedExchange_FanoutBroadcast_ShouldDeliverToAllQueues()
    {
        // Arrange
        var exchangeName = CreateUniqueExchangeName("fanout-test");
        var queues = Enumerable.Range(1, 4)
            .Select(i => CreateUniqueQueueName($"broadcast-{i}"))
            .ToList();

        // Setup fanout exchange
        await QueueProvider.DeclareExchangeAsync(exchangeName, "fanout", isDurable: false);

        foreach (var queue in queues)
        {
            await QueueProvider.DeclareQueueAsync(queue, isDurable: false);
            await QueueProvider.BindQueueAsync(queue, exchangeName, ""); // Fanout ignores routing key
        }

        var receivedByQueue = new ConcurrentDictionary<string, List<UserActivityMessage>>();
        var allBroadcast = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing fanout broadcast to {queues.Count} queues: {exchangeName}");

        // Start consumers for all queues
        var consumers = new List<Task>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        foreach (var queue in queues)
        {
            receivedByQueue[queue] = new List<UserActivityMessage>();

            var consumer = QueueProvider.StartConsumingAsync<UserActivityMessage>(
                async (msg) =>
                {
                    lock (receivedByQueue)
                    {
                        receivedByQueue[queue].Add(msg);
                        _output.WriteLine($"Queue {queue} received: {msg.Action}");

                        // Check if all queues have received the message
                        var allReceived = receivedByQueue.Values.All(list => list.Count >= 1);
                        if (allReceived)
                        {
                            allBroadcast.TrySetResult(true);
                            cts.Cancel();
                        }
                    }
                    return true;
                },
                queueName: queue,
                cancellationToken: cts.Token);

            consumers.Add(consumer);
        }

        await Task.Delay(1000);

        // Act - Publish single message to fanout exchange
        var broadcastMessage = new UserActivityMessage
        {
            Action = "broadcast.event",
            UserId = "test-user"
        };

        await QueueProvider.PublishAsync(broadcastMessage,
            exchangeName: exchangeName, routingKey: "ignored");

        _output.WriteLine("✅ Broadcast message published to fanout exchange");

        await allBroadcast.Task;
        await Task.WhenAll(consumers);

        // Assert - All queues should receive the same message
        foreach (var queue in queues)
        {
            var messages = receivedByQueue[queue];
            messages.Should().HaveCount(1, $"Queue {queue} should receive exactly 1 broadcast message");
            messages[0].Id.Should().Be(broadcastMessage.Id, $"Queue {queue} should receive the correct message");
            messages[0].Action.Should().Be("broadcast.event");
        }

        _output.WriteLine($"✅ Fanout broadcast validated: all {queues.Count} queues received the message");
    }

    #endregion

    #region Timeout and Cancellation Tests

    [Fact]
    public async Task TimeoutHandling_PublishWithTimeout_ShouldRespectCancellation()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("timeout-test");
        var message = new QueueMessage { Content = "Timeout test message" };
        var shortTimeout = TimeSpan.FromMilliseconds(1); // Very short timeout

        _output.WriteLine("Testing publish timeout and cancellation");

        // Act & Assert - Very short timeout should complete normally for local container
        using var cts = new CancellationTokenSource(shortTimeout);

        // This should actually succeed with local container despite short timeout
        var result = await QueueProvider.PublishAsync(message, queueName, cancellationToken: cts.Token);
        result.Should().BeTrue();

        _output.WriteLine("✅ Publish with short timeout completed (local container is fast)");

        // Test pre-cancelled token
        var cancelledCts = new CancellationTokenSource();
        cancelledCts.Cancel();

        var act = async () => await QueueProvider.PublishAsync(message, queueName,
            cancellationToken: cancelledCts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        _output.WriteLine("✅ Pre-cancelled token properly throws OperationCanceledException");
    }

    [Fact]
    public async Task TimeoutHandling_ConsumerCancellation_ShouldStopGracefully()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("consumer-timeout");
        var messageReceived = false;
        var consumerStopped = false;

        _output.WriteLine($"Testing consumer cancellation on queue: {queueName}");

        // Start consumer with short timeout
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                messageReceived = true;
                _output.WriteLine($"Message received: {msg.Content}");
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Publish a message
        await QueueProvider.PublishAsync(new QueueMessage { Content = "Before cancellation" }, queueName);

        // Wait for timeout
        try
        {
            await consumerTask;
            consumerStopped = true;
        }
        catch (OperationCanceledException)
        {
            consumerStopped = true;
            _output.WriteLine("Consumer stopped due to cancellation (expected)");
        }

        // Assert
        messageReceived.Should().BeTrue("Message should be received before cancellation");
        consumerStopped.Should().BeTrue("Consumer should stop when cancelled");

        _output.WriteLine("✅ Consumer cancellation handled gracefully");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task EdgeCase_EmptyMessage_ShouldHandleGracefully()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("empty-message");
        var emptyMessage = new QueueMessage(); // Default values only

        var receivedMessage = default(QueueMessage);
        var messageReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing empty message handling on queue: {queueName}");

        // Start consumer
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                receivedMessage = msg;
                messageReceived.TrySetResult(true);
                cts.Cancel();
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act
        await QueueProvider.PublishAsync(emptyMessage, queueName);

        await messageReceived.Task;
        await consumerTask;

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Id.Should().Be(emptyMessage.Id);
        receivedMessage.Content.Should().Be(string.Empty);

        _output.WriteLine("✅ Empty message handled gracefully");
    }

    [Fact]
    public async Task EdgeCase_VeryLargeMessage_ShouldHandleWithinLimits()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("large-message");
        var largeContent = new string('X', 100000); // 100KB content
        var largeMessage = new QueueMessage { Content = largeContent };

        var receivedMessage = default(QueueMessage);
        var messageReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing large message ({largeContent.Length:N0} characters) on queue: {queueName}");

        // Start consumer
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                receivedMessage = msg;
                messageReceived.TrySetResult(true);
                cts.Cancel();
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await QueueProvider.PublishAsync(largeMessage, queueName);

        await messageReceived.Task;
        await consumerTask;
        stopwatch.Stop();

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Id.Should().Be(largeMessage.Id);
        receivedMessage.Content.Length.Should().Be(largeContent.Length);
        receivedMessage.Content.Should().Be(largeContent);

        _output.WriteLine($"✅ Large message handled successfully in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task EdgeCase_SpecialCharacters_ShouldPreserveContent()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("special-chars");
        var specialContent = "Test with émojis 🚀, unicode chars αβγ, and symbols !@#$%^&*(){}[]|\\\"'<>?/";
        var specialMessage = new QueueMessage { Content = specialContent };

        var receivedMessage = default(QueueMessage);
        var messageReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing special characters: {specialContent}");

        // Start consumer
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                receivedMessage = msg;
                messageReceived.TrySetResult(true);
                cts.Cancel();
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act
        await QueueProvider.PublishAsync(specialMessage, queueName);

        await messageReceived.Task;
        await consumerTask;

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Content.Should().Be(specialContent);

        _output.WriteLine("✅ Special characters preserved correctly");
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public async Task ResourceCleanup_MultipleProviderInstances_ShouldDisposeCorrectly()
    {
        // Arrange
        var providers = new List<IQueueProvider>();
        var queueName = CreateUniqueQueueName("cleanup-multiple");

        _output.WriteLine("Testing multiple provider instance cleanup");

        // Create multiple provider instances
        for (int i = 0; i < 3; i++)
        {
            var rest = new Rest(ConnectionString, new RestConfig
            {
                OperationMode = RestMode.RabbitMq,
                AuthKey = "testuser",
                AuthSecret = "testpass"
            });

            var provider = rest.GetProvider<IQueueProvider>();
            provider.Should().NotBeNull();
            providers.Add(provider!);

            // Test each provider works
            await provider!.DeclareQueueAsync($"{queueName}-{i}", isDurable: false);
            _output.WriteLine($"✅ Provider {i + 1} created and tested");
        }

        // Act - Dispose all providers
        foreach (var provider in providers.OfType<IDisposable>())
        {
            provider.Dispose();
        }

        _output.WriteLine("✅ All provider instances disposed");

        // Assert - Original provider should still work
        var testQueue = CreateUniqueQueueName("cleanup-verify");
        var result = await QueueProvider.DeclareQueueAsync(testQueue, isDurable: false);
        result.Should().NotBeNullOrEmpty("Original provider should still work after disposing others");

        _output.WriteLine("✅ Resource cleanup validated - original provider still functional");
    }

    #endregion
}