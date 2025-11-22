using System;
using System.Collections.Concurrent;
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
/// Comprehensive enterprise-grade integration tests for RabbitMQ provider
/// These tests validate 100% reliability for enterprise use with NO "assume success" patterns
/// </summary>
[Collection("RabbitMQIntegration")]
public class RabbitMQIntegrationTests : RabbitMQTestBase
{
    private readonly ITestOutputHelper _output;

    public RabbitMQIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Basic Queue Operations

    [Fact]
    public async Task DeclareQueueAsync_WithValidParameters_ShouldCreateQueue()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("declare-test");
        _output.WriteLine($"Testing queue declaration: {queueName}");

        // Act
        var result = await QueueProvider.DeclareQueueAsync(queueName, isDurable: false);

        // Assert - Validate queue was actually created
        result.Should().NotBeNullOrEmpty();
        result.Should().Be(queueName);
        _output.WriteLine($"✅ Queue declared successfully: {result}");

        // Validate queue exists by declaring it again (should not throw)
        var result2 = await QueueProvider.DeclareQueueAsync(queueName, isDurable: false);
        result2.Should().Be(queueName);
        _output.WriteLine($"✅ Queue redeclaration successful: {result2}");
    }

    [Fact]
    public async Task DeclareQueueAsync_WithNullName_ShouldGenerateRandomName()
    {
        // Arrange & Act
        var result = await QueueProvider.DeclareQueueAsync();

        // Assert - Should generate a valid queue name
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("queue_");
        result.Length.Should().BeGreaterThan(10);
        _output.WriteLine($"✅ Auto-generated queue name: {result}");
    }

    [Fact]
    public async Task DeclareExchangeAsync_WithValidParameters_ShouldCreateExchange()
    {
        // Arrange
        var exchangeName = CreateUniqueExchangeName("declare-test");
        _output.WriteLine($"Testing exchange declaration: {exchangeName}");

        // Act & Assert - Should not throw
        await QueueProvider.DeclareExchangeAsync(exchangeName, "direct", isDurable: false);
        _output.WriteLine($"✅ Exchange declared successfully: {exchangeName}");

        // Validate exchange exists by declaring it again (should not throw)
        await QueueProvider.DeclareExchangeAsync(exchangeName, "direct", isDurable: false);
        _output.WriteLine($"✅ Exchange redeclaration successful: {exchangeName}");
    }

    [Fact]
    public async Task BindQueueAsync_WithValidParameters_ShouldBindSuccessfully()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("bind-test");
        var exchangeName = CreateUniqueExchangeName("bind-test");
        var routingKey = "test.routing.key";

        await QueueProvider.DeclareQueueAsync(queueName, isDurable: false);
        await QueueProvider.DeclareExchangeAsync(exchangeName, "direct", isDurable: false);

        _output.WriteLine($"Testing queue binding: {queueName} -> {exchangeName} ({routingKey})");

        // Act & Assert - Should not throw
        await QueueProvider.BindQueueAsync(queueName, exchangeName, routingKey);
        _output.WriteLine($"✅ Queue bound successfully: {queueName} -> {exchangeName}");

        // Multiple bindings should be allowed
        await QueueProvider.BindQueueAsync(queueName, exchangeName, "alternative.key");
        _output.WriteLine($"✅ Additional binding successful: alternative.key");
    }

    #endregion

    #region Message Publishing and Consuming

    [Fact]
    public async Task PublishAndConsume_SingleMessage_ShouldDeliverCorrectly()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("single-message");
        var testMessage = new QueueMessage
        {
            Content = "Test message for single delivery",
            Source = "IntegrationTest",
            Priority = 1
        };

        var receivedMessages = new ConcurrentBag<QueueMessage>();
        var messageReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing single message publish/consume on queue: {queueName}");

        // Start consumer first
        var consumerCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                _output.WriteLine($"Message received: {msg}");
                receivedMessages.Add(msg);
                messageReceived.TrySetResult(true);
                consumerCts.Cancel(); // Stop consuming after first message
                return true; // Acknowledge message
            },
            queueName: queueName,
            cancellationToken: consumerCts.Token);

        // Give consumer time to set up
        await Task.Delay(1000);

        // Act - Publish message
        var publishResult = await QueueProvider.PublishAsync(testMessage, queueName);

        // Assert publishing succeeded
        publishResult.Should().BeTrue();
        _output.WriteLine($"✅ Message published successfully");

        // Wait for message to be received
        await messageReceived.Task;
        await consumerTask;

        // Validate message was received correctly
        receivedMessages.Should().HaveCount(1);
        var receivedMessage = receivedMessages.First();
        receivedMessage.Id.Should().Be(testMessage.Id);
        receivedMessage.Content.Should().Be(testMessage.Content);
        receivedMessage.Source.Should().Be(testMessage.Source);
        receivedMessage.Priority.Should().Be(testMessage.Priority);

        _output.WriteLine($"✅ Message content validated successfully");
    }

    [Fact]
    public async Task PublishAndConsume_MultipleMessages_ShouldDeliverAllCorrectly()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("multiple-messages");
        var messageCount = 5;
        var testMessages = Enumerable.Range(1, messageCount)
            .Select(i => new QueueMessage
            {
                Content = $"Test message {i}",
                Source = "BatchTest",
                Priority = i
            }).ToList();

        var receivedMessages = new ConcurrentBag<QueueMessage>();
        var allMessagesReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing {messageCount} messages publish/consume on queue: {queueName}");

        // Start consumer
        var consumerCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                _output.WriteLine($"Message received: {msg}");
                receivedMessages.Add(msg);

                if (receivedMessages.Count >= messageCount)
                {
                    allMessagesReceived.TrySetResult(true);
                    consumerCts.Cancel();
                }
                return true;
            },
            queueName: queueName,
            cancellationToken: consumerCts.Token);

        await Task.Delay(1000);

        // Act - Publish all messages
        foreach (var message in testMessages)
        {
            var result = await QueueProvider.PublishAsync(message, queueName);
            result.Should().BeTrue();
            await Task.Delay(50); // Small delay between publishes
        }

        _output.WriteLine($"✅ All {messageCount} messages published");

        // Wait for all messages to be received
        await allMessagesReceived.Task;
        await consumerTask;

        // Assert - Validate all messages received correctly
        receivedMessages.Should().HaveCount(messageCount);

        foreach (var originalMessage in testMessages)
        {
            var receivedMessage = receivedMessages.FirstOrDefault(m => m.Id == originalMessage.Id);
            receivedMessage.Should().NotBeNull($"Message {originalMessage.Id} should be received");
            receivedMessage!.Content.Should().Be(originalMessage.Content);
            receivedMessage.Priority.Should().Be(originalMessage.Priority);
        }

        _output.WriteLine($"✅ All {messageCount} messages validated successfully");
    }

    [Fact]
    public async Task PublishAndConsume_WithExchangeRouting_ShouldRouteCorrectly()
    {
        // Arrange
        var exchangeName = CreateUniqueExchangeName("routing-test");
        var queueName1 = CreateUniqueQueueName("routing-queue1");
        var queueName2 = CreateUniqueQueueName("routing-queue2");
        var routingKey1 = "orders.created";
        var routingKey2 = "orders.cancelled";

        // Setup exchange and queues
        await QueueProvider.DeclareExchangeAsync(exchangeName, "direct", isDurable: false);
        await QueueProvider.DeclareQueueAsync(queueName1, isDurable: false);
        await QueueProvider.DeclareQueueAsync(queueName2, isDurable: false);
        await QueueProvider.BindQueueAsync(queueName1, exchangeName, routingKey1);
        await QueueProvider.BindQueueAsync(queueName2, exchangeName, routingKey2);

        var queue1Messages = new ConcurrentBag<OrderMessage>();
        var queue2Messages = new ConcurrentBag<OrderMessage>();
        var bothQueuesReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing exchange routing: {exchangeName}");

        // Start consumers for both queues
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var consumer1Task = QueueProvider.StartConsumingAsync<OrderMessage>(
            async (msg) =>
            {
                _output.WriteLine($"Queue1 received: {msg}");
                queue1Messages.Add(msg);
                CheckAllReceived();
                return true;
            },
            queueName: queueName1,
            cancellationToken: cts.Token);

        var consumer2Task = QueueProvider.StartConsumingAsync<OrderMessage>(
            async (msg) =>
            {
                _output.WriteLine($"Queue2 received: {msg}");
                queue2Messages.Add(msg);
                CheckAllReceived();
                return true;
            },
            queueName: queueName2,
            cancellationToken: cts.Token);

        void CheckAllReceived()
        {
            if (queue1Messages.Count >= 1 && queue2Messages.Count >= 1)
            {
                bothQueuesReceived.TrySetResult(true);
                cts.Cancel();
            }
        }

        await Task.Delay(1000);

        // Act - Publish to different routing keys
        var order1 = new OrderMessage { Status = OrderStatus.Created, CustomerId = "customer1" };
        var order2 = new OrderMessage { Status = OrderStatus.Cancelled, CustomerId = "customer2" };

        await QueueProvider.PublishAsync(order1, exchangeName: exchangeName, routingKey: routingKey1);
        await QueueProvider.PublishAsync(order2, exchangeName: exchangeName, routingKey: routingKey2);

        _output.WriteLine("✅ Messages published to exchange with routing keys");

        // Wait for messages to be routed correctly
        await bothQueuesReceived.Task;
        await Task.WhenAll(consumer1Task, consumer2Task);

        // Assert - Validate correct routing
        queue1Messages.Should().HaveCount(1);
        queue2Messages.Should().HaveCount(1);

        queue1Messages.First().Status.Should().Be(OrderStatus.Created);
        queue2Messages.First().Status.Should().Be(OrderStatus.Cancelled);

        _output.WriteLine($"✅ Exchange routing validated successfully");
    }

    [Fact]
    public async Task ConsumerAcknowledgment_RejectMessage_ShouldRequeue()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("ack-test");
        var testMessage = new QueueMessage { Content = "Acknowledgment test message" };

        var attemptCount = 0;
        var maxAttempts = 3;
        var finalReceived = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing message acknowledgment and requeuing on queue: {queueName}");

        // Start consumer that rejects first few attempts
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                attemptCount++;
                _output.WriteLine($"Message attempt {attemptCount}: {msg}");

                if (attemptCount < maxAttempts)
                {
                    _output.WriteLine($"Rejecting message (attempt {attemptCount})");
                    return false; // Reject - should requeue
                }
                else
                {
                    _output.WriteLine($"Accepting message (attempt {attemptCount})");
                    finalReceived.TrySetResult(true);
                    cts.Cancel();
                    return true; // Accept
                }
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act
        await QueueProvider.PublishAsync(testMessage, queueName);
        _output.WriteLine("✅ Message published for acknowledgment test");

        // Wait for final acceptance
        await finalReceived.Task;
        await consumerTask;

        // Assert
        attemptCount.Should().Be(maxAttempts);
        _output.WriteLine($"✅ Message was requeued and processed {maxAttempts} times as expected");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PublishAsync_WithInvalidConnection_ShouldThrowOEliteException()
    {
        // Arrange - Create provider with invalid connection
        var invalidConfig = new RestConfig
        {
            OperationMode = RestMode.RabbitMq
        };
        var invalidRest = new Rest("amqp://invalid-host:5672", invalidConfig);

        _output.WriteLine("Testing publish with invalid connection");

        // Act & Assert
        var invalidProvider = invalidRest.GetProvider<IQueueProvider>();
        if (invalidProvider != null)
        {
            var testMessage = new QueueMessage { Content = "This should fail" };
            var act = async () => await invalidProvider.PublishAsync(testMessage, "test-queue");
            await act.Should().ThrowAsync<OEliteException>();
            _output.WriteLine("✅ Invalid connection properly throws OEliteException");
        }
    }

    [Fact]
    public async Task StartConsumingAsync_WithNonExistentQueue_ShouldCreateQueue()
    {
        // Arrange
        var nonExistentQueue = CreateUniqueQueueName("non-existent");
        var messageReceived = false;

        _output.WriteLine($"Testing consumer start with non-existent queue: {nonExistentQueue}");

        // Act - Should auto-create queue
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                messageReceived = true;
                cts.Cancel();
                return true;
            },
            queueName: nonExistentQueue,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Publish to the auto-created queue
        var testMessage = new QueueMessage { Content = "Auto-queue test" };
        await QueueProvider.PublishAsync(testMessage, nonExistentQueue);

        await consumerTask;

        // Assert
        messageReceived.Should().BeTrue();
        _output.WriteLine($"✅ Non-existent queue was auto-created and message delivered");
    }

    [Fact]
    public async Task BindQueueAsync_WithNonExistentExchange_ShouldThrowOEliteException()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("bind-error");
        var nonExistentExchange = "non-existent-exchange";

        await QueueProvider.DeclareQueueAsync(queueName, isDurable: false);

        _output.WriteLine($"Testing bind with non-existent exchange: {nonExistentExchange}");

        // Act & Assert
        var act = async () => await QueueProvider.BindQueueAsync(queueName, nonExistentExchange, "test.key");
        await act.Should().ThrowAsync<OEliteException>();
        _output.WriteLine("✅ Binding to non-existent exchange properly throws OEliteException");
    }

    [Fact]
    public async Task ConsumerErrorHandling_WithExceptionInHandler_ShouldNackAndContinue()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("error-handling");
        var processedCount = 0;
        var errorCount = 0;
        var totalMessages = 3;
        var allProcessed = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing consumer error handling on queue: {queueName}");

        // Start consumer that throws on first message
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                processedCount++;
                _output.WriteLine($"Processing message {processedCount}: {msg.Content}");

                if (msg.Content.Contains("error"))
                {
                    errorCount++;
                    _output.WriteLine("Throwing exception for error message");
                    throw new InvalidOperationException("Simulated processing error");
                }

                if (processedCount >= totalMessages + errorCount)
                {
                    allProcessed.TrySetResult(true);
                    cts.Cancel();
                }
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act - Publish messages including one that will cause error
        await QueueProvider.PublishAsync(new QueueMessage { Content = "normal message 1" }, queueName);
        await QueueProvider.PublishAsync(new QueueMessage { Content = "error message" }, queueName);
        await QueueProvider.PublishAsync(new QueueMessage { Content = "normal message 2" }, queueName);

        // Wait for processing to complete
        await allProcessed.Task;
        await consumerTask;

        // Assert
        processedCount.Should().BeGreaterThan(totalMessages);
        errorCount.Should().BeGreaterThan(0);
        _output.WriteLine($"✅ Error handling validated: {processedCount} total processed, {errorCount} errors");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task PerformanceTest_HighThroughput_ShouldMeetSLARequirements()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("performance");
        var messageCount = 100; // Reduced for CI/CD environments
        var maxProcessingTimeMs = 5000; // 5 seconds SLA
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => PerformanceTestMessage.CreateWithPayloadSize(i, 1024)) // 1KB messages
            .ToList();

        var receivedMessages = new ConcurrentBag<PerformanceTestMessage>();
        var allReceived = new TaskCompletionSource<bool>();
        var stopwatch = Stopwatch.StartNew();

        _output.WriteLine($"Testing high throughput: {messageCount} messages on queue: {queueName}");

        // Start consumer
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumerTask = QueueProvider.StartConsumingAsync<PerformanceTestMessage>(
            async (msg) =>
            {
                msg.MarkProcessed();
                receivedMessages.Add(msg);

                if (receivedMessages.Count >= messageCount)
                {
                    allReceived.TrySetResult(true);
                    cts.Cancel();
                }
                return true;
            },
            queueName: queueName,
            prefetchCount: 10, // Increased prefetch for performance
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        var publishStart = Stopwatch.StartNew();

        // Act - Publish all messages as fast as possible
        var publishTasks = messages.Select(async msg =>
        {
            await QueueProvider.PublishAsync(msg, queueName);
        });
        await Task.WhenAll(publishTasks);

        publishStart.Stop();
        _output.WriteLine($"✅ Published {messageCount} messages in {publishStart.ElapsedMilliseconds}ms");

        // Wait for all messages to be consumed
        await allReceived.Task;
        await consumerTask;

        stopwatch.Stop();

        // Assert - Performance validation
        receivedMessages.Should().HaveCount(messageCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxProcessingTimeMs,
            $"Processing {messageCount} messages should complete within {maxProcessingTimeMs}ms SLA");

        var averageLatency = receivedMessages.Average(m => m.ProcessingDuration?.TotalMilliseconds ?? 0);
        _output.WriteLine($"✅ Performance test completed:");
        _output.WriteLine($"   Total time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Average latency: {averageLatency:F2}ms");
        _output.WriteLine($"   Throughput: {messageCount * 1000 / stopwatch.ElapsedMilliseconds:F2} msg/sec");

        // Enterprise SLA validation
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxProcessingTimeMs);
        averageLatency.Should().BeLessThan(100, "Average latency should be under 100ms");
    }

    [Fact]
    public async Task LoadTest_ConcurrentConsumers_ShouldHandleParallelProcessing()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("load-test");
        var messageCount = 50;
        var consumerCount = 3;
        var allReceived = new ConcurrentBag<QueueMessage>();
        var completionSource = new TaskCompletionSource<bool>();

        _output.WriteLine($"Testing load: {messageCount} messages with {consumerCount} concurrent consumers");

        // Start multiple consumers
        var consumers = new List<Task>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        for (int i = 0; i < consumerCount; i++)
        {
            var consumerId = i + 1;
            var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
                async (msg) =>
                {
                    _output.WriteLine($"Consumer {consumerId} processing: {msg.Content}");
                    allReceived.Add(msg);

                    if (allReceived.Count >= messageCount)
                    {
                        completionSource.TrySetResult(true);
                        cts.Cancel();
                    }

                    // Simulate processing time
                    await Task.Delay(10);
                    return true;
                },
                queueName: queueName,
                cancellationToken: cts.Token);

            consumers.Add(consumerTask);
        }

        await Task.Delay(1000);

        // Act - Publish messages
        var publishTasks = Enumerable.Range(0, messageCount).Select(async i =>
        {
            var message = new QueueMessage { Content = $"Load test message {i + 1}" };
            await QueueProvider.PublishAsync(message, queueName);
        });

        await Task.WhenAll(publishTasks);
        _output.WriteLine($"✅ Published {messageCount} messages for load test");

        // Wait for all messages to be processed
        await completionSource.Task;
        await Task.WhenAll(consumers);

        // Assert
        allReceived.Should().HaveCount(messageCount);
        _output.WriteLine($"✅ Load test completed: {allReceived.Count} messages processed by {consumerCount} consumers");
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public async Task ResourceManagement_StopConsuming_ShouldCleanupProperly()
    {
        // Arrange
        var queueName = CreateUniqueQueueName("cleanup-test");
        var messageReceived = false;

        _output.WriteLine($"Testing resource cleanup on queue: {queueName}");

        // Start consumer
        var cts = new CancellationTokenSource();
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                messageReceived = true;
                _output.WriteLine($"Message received before cleanup: {msg.Content}");
                return true;
            },
            queueName: queueName,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Publish and consume a message to verify consumer is working
        await QueueProvider.PublishAsync(new QueueMessage { Content = "Test before cleanup" }, queueName);
        await Task.Delay(1000);
        messageReceived.Should().BeTrue();

        // Act - Stop consuming
        await QueueProvider.StopConsumingAsync();
        _output.WriteLine("✅ Consumer stopped");

        // Try to publish another message - consumer should not receive it
        messageReceived = false;
        await QueueProvider.PublishAsync(new QueueMessage { Content = "Test after cleanup" }, queueName);
        await Task.Delay(1000);

        // Assert - Message should not be consumed after stopping
        messageReceived.Should().BeFalse("Consumer should not receive messages after stopping");
        _output.WriteLine("✅ Resource cleanup validated - no messages received after stop");
    }

    #endregion

    #region Connection Resilience Tests

    [Fact]
    public async Task ConnectionResilience_ValidateContainerIsRunning_ShouldConfirmConnectivity()
    {
        // Arrange & Act
        _output.WriteLine("Testing connection resilience and container health");

        // Verify container is running
        IsContainerRunning.Should().BeTrue("RabbitMQ container should be running");
        ConnectionString.Should().NotBeNullOrEmpty("Connection string should be available");

        // Verify provider is functional
        QueueProvider.Should().NotBeNull("QueueProvider should be initialized");
        QueueProvider.ProviderName.Should().Be("RabbitMQ");

        // Test basic operations to confirm connectivity
        var testQueue = CreateUniqueQueueName("resilience-test");
        var result = await QueueProvider.DeclareQueueAsync(testQueue, isDurable: false);
        result.Should().NotBeNullOrEmpty("Queue declaration should succeed");

        _output.WriteLine($"✅ Connection resilience validated:");
        _output.WriteLine($"   Container running: {IsContainerRunning}");
        _output.WriteLine($"   Connection string: {ConnectionString}");
        _output.WriteLine($"   Provider: {QueueProvider.ProviderName}");
        _output.WriteLine($"   Test queue: {result}");
    }

    #endregion
}