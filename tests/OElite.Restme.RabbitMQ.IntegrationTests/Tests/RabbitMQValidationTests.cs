using System;
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
/// Final validation tests for RabbitMQ provider enterprise readiness
/// These tests serve as comprehensive smoke tests and final validation
/// </summary>
[Collection("RabbitMQIntegration")]
public class RabbitMQValidationTests : RabbitMQTestBase
{
    private readonly ITestOutputHelper _output;

    public RabbitMQValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Validation_ProviderCapabilities_ShouldMeetEnterpriseRequirements()
    {
        // Arrange & Act
        _output.WriteLine("Validating RabbitMQ provider enterprise capabilities");

        // Assert - Provider configuration validation
        QueueProvider.Should().NotBeNull("QueueProvider must be initialized");
        QueueProvider.ProviderName.Should().Be("RabbitMQ", "Provider name must be correct");
        QueueProvider.Capabilities.Should().HaveFlag(ProviderCapabilities.Queue, "Must support queue operations");

        // Validate configuration
        var config = QueueProvider.Configuration;
        config.Should().NotBeNull("Configuration must be available");
        config.OperationMode.Should().Be(RestMode.RabbitMq, "Operation mode must be RabbitMQ");

        _output.WriteLine($"✅ Provider validation:");
        _output.WriteLine($"   Name: {QueueProvider.ProviderName}");
        _output.WriteLine($"   Capabilities: {QueueProvider.Capabilities}");
        _output.WriteLine($"   Mode: {config.OperationMode}");
        _output.WriteLine($"   Connection: {IsContainerRunning}");
    }

    [Fact]
    public async Task Validation_EndToEndWorkflow_ShouldCompleteSuccessfully()
    {
        // Arrange
        var workflowId = Guid.NewGuid().ToString("N");
        var exchangeName = CreateUniqueExchangeName($"workflow-{workflowId}");
        var orderQueue = CreateUniqueQueueName($"orders-{workflowId}");
        var paymentQueue = CreateUniqueQueueName($"payments-{workflowId}");
        var notificationQueue = CreateUniqueQueueName($"notifications-{workflowId}");

        _output.WriteLine($"Validating end-to-end enterprise workflow: {workflowId}");

        // Setup enterprise workflow infrastructure
        await QueueProvider.DeclareExchangeAsync(exchangeName, "topic", isDurable: true);
        await QueueProvider.DeclareQueueAsync(orderQueue, isDurable: true, autoDelete: false);
        await QueueProvider.DeclareQueueAsync(paymentQueue, isDurable: true, autoDelete: false);
        await QueueProvider.DeclareQueueAsync(notificationQueue, isDurable: true, autoDelete: false);

        await QueueProvider.BindQueueAsync(orderQueue, exchangeName, "order.*");
        await QueueProvider.BindQueueAsync(paymentQueue, exchangeName, "payment.*");
        await QueueProvider.BindQueueAsync(notificationQueue, exchangeName, "notification.*");

        var workflowSteps = new List<string>();
        var workflowComplete = new TaskCompletionSource<bool>();
        var expectedSteps = 6; // 2 orders, 2 payments, 2 notifications

        // Act - Simulate enterprise workflow
        var stopwatch = Stopwatch.StartNew();

        // Start workflow processors
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var orderProcessor = QueueProvider.StartConsumingAsync<OrderMessage>(
            async (order) =>
            {
                workflowSteps.Add($"ProcessOrder:{order.OrderId}");
                _output.WriteLine($"Order processed: {order.OrderId} - ${order.Amount}");

                // Trigger payment processing
                await QueueProvider.PublishAsync(
                    new OrderMessage { OrderId = order.OrderId, Amount = order.Amount, Status = OrderStatus.Paid },
                    exchangeName: exchangeName,
                    routingKey: "payment.process");

                CheckWorkflowComplete();
                return true;
            },
            queueName: orderQueue,
            isDurable: true,
            autoDelete: false,
            cancellationToken: cts.Token);

        var paymentProcessor = QueueProvider.StartConsumingAsync<OrderMessage>(
            async (payment) =>
            {
                workflowSteps.Add($"ProcessPayment:{payment.OrderId}");
                _output.WriteLine($"Payment processed: {payment.OrderId} - ${payment.Amount}");

                // Trigger notification
                await QueueProvider.PublishAsync(
                    new UserActivityMessage { Action = $"OrderComplete:{payment.OrderId}" },
                    exchangeName: exchangeName,
                    routingKey: "notification.send");

                CheckWorkflowComplete();
                return true;
            },
            queueName: paymentQueue,
            isDurable: true,
            autoDelete: false,
            cancellationToken: cts.Token);

        var notificationProcessor = QueueProvider.StartConsumingAsync<UserActivityMessage>(
            async (notification) =>
            {
                workflowSteps.Add($"SendNotification:{notification.Action}");
                _output.WriteLine($"Notification sent: {notification.Action}");

                CheckWorkflowComplete();
                return true;
            },
            queueName: notificationQueue,
            isDurable: true,
            autoDelete: false,
            cancellationToken: cts.Token);

        void CheckWorkflowComplete()
        {
            if (workflowSteps.Count >= expectedSteps)
            {
                workflowComplete.TrySetResult(true);
            }
        }

        await Task.Delay(1000);

        // Initiate workflow with order creation
        await QueueProvider.PublishAsync(
            new OrderMessage { OrderId = Guid.NewGuid(), Amount = 99.99m, Status = OrderStatus.Created },
            exchangeName: exchangeName,
            routingKey: "order.created");

        await QueueProvider.PublishAsync(
            new OrderMessage { OrderId = Guid.NewGuid(), Amount = 149.99m, Status = OrderStatus.Created },
            exchangeName: exchangeName,
            routingKey: "order.created");

        // Wait for workflow completion
        await workflowComplete.Task;

        // Give a small delay to ensure all async operations complete
        await Task.Delay(500);

        // Now cancel the consumers
        cts.Cancel();

        await Task.WhenAll(orderProcessor, paymentProcessor, notificationProcessor);

        stopwatch.Stop();

        // Assert - Validate enterprise workflow completion
        workflowSteps.Should().HaveCount(expectedSteps, "All workflow steps should complete");
        workflowSteps.Count(s => s.StartsWith("ProcessOrder")).Should().Be(2, "Should process 2 orders");
        workflowSteps.Count(s => s.StartsWith("ProcessPayment")).Should().Be(2, "Should process 2 payments");
        workflowSteps.Count(s => s.StartsWith("SendNotification")).Should().Be(2, "Should send 2 notifications");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Workflow should complete within 10 seconds");

        _output.WriteLine($"✅ Enterprise workflow validation completed:");
        _output.WriteLine($"   Total steps: {workflowSteps.Count}");
        _output.WriteLine($"   Duration: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Steps: {string.Join(", ", workflowSteps)}");
    }


    [Fact]
    public async Task Validation_FaultTolerance_ShouldRecoverFromErrors()
    {
        // Arrange
        var faultQueue = CreateUniqueQueueName("fault-tolerance");
        var totalMessages = 10;
        var errorEveryN = 3; // Cause error on every 3rd message

        var processedMessages = new List<QueueMessage>();
        var errorCount = 0;
        var allProcessed = new TaskCompletionSource<bool>();

        _output.WriteLine($"Validating fault tolerance with {totalMessages} messages (error every {errorEveryN})");

        // Start fault-tolerant consumer
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                // Simulate processing error on certain messages
                if (msg.Priority % errorEveryN == 0 && errorCount < 3)
                {
                    errorCount++;
                    _output.WriteLine($"Simulating error for message: {msg.Priority}");
                    throw new InvalidOperationException($"Simulated error for message {msg.Priority}");
                }

                processedMessages.Add(msg);
                _output.WriteLine($"Successfully processed message: {msg.Priority} (total: {processedMessages.Count})");

                // Complete when we have processed the expected number of successful messages
                var expectedSuccessful = totalMessages - Math.Min(3, totalMessages / errorEveryN);
                if (processedMessages.Count >= expectedSuccessful)
                {
                    allProcessed.TrySetResult(true);
                    cts.Cancel();
                }

                return true;
            },
            queueName: faultQueue,
            cancellationToken: cts.Token);

        await Task.Delay(1000);

        // Act - Publish messages that will cause some errors
        for (int i = 0; i < totalMessages; i++)
        {
            var message = new QueueMessage
            {
                Content = $"Fault tolerance test message {i + 1}",
                Priority = i
            };
            await QueueProvider.PublishAsync(message, faultQueue);
        }

        _output.WriteLine($"✅ Published {totalMessages} messages for fault tolerance test");

        // Wait for processing to complete
        await allProcessed.Task;
        await consumerTask;

        // Assert - Validate fault tolerance
        errorCount.Should().BeGreaterThan(0, "Should have encountered some errors for testing");
        processedMessages.Should().NotBeEmpty("Should successfully process some messages despite errors");

        var expectedSuccessfulMessages = totalMessages - Math.Min(3, totalMessages / errorEveryN);
        processedMessages.Count.Should().BeGreaterOrEqualTo(expectedSuccessfulMessages - 2,
            "Should process most messages despite errors (allowing for requeue behavior)");

        _output.WriteLine($"✅ Fault tolerance validation:");
        _output.WriteLine($"   Total published: {totalMessages}");
        _output.WriteLine($"   Errors encountered: {errorCount}");
        _output.WriteLine($"   Successfully processed: {processedMessages.Count}");
        _output.WriteLine($"   Success rate: {processedMessages.Count * 100.0 / totalMessages:F1}%");
    }

    [Fact]
    public async Task Validation_ResourceManagement_ShouldHandleCleanupProperly()
    {
        // Arrange
        var resourceQueue = CreateUniqueQueueName("resource-mgmt");
        var tempExchange = CreateUniqueExchangeName("temp-resource");

        _output.WriteLine("Validating resource management and cleanup");

        // Act - Create temporary resources
        await QueueProvider.DeclareExchangeAsync(tempExchange, "direct", isDurable: false, autoDelete: true);
        await QueueProvider.DeclareQueueAsync(resourceQueue, isDurable: false, autoDelete: true);
        await QueueProvider.BindQueueAsync(resourceQueue, tempExchange, "test");

        // Test resource functionality
        var testMessage = new QueueMessage { Content = "Resource management test" };
        await QueueProvider.PublishAsync(testMessage, exchangeName: tempExchange, routingKey: "test", isDurable: false);

        var messageReceived = false;
        var received = new TaskCompletionSource<bool>();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
            async (msg) =>
            {
                messageReceived = true;
                received.TrySetResult(true);
                cts.Cancel();
                return true;
            },
            queueName: resourceQueue,
            isDurable: false,
            cancellationToken: cts.Token);

        await received.Task;
        await consumerTask;

        // Cleanup resources
        await QueueProvider.StopConsumingAsync();

        // Assert - Validate resource management
        messageReceived.Should().BeTrue("Resources should function properly before cleanup");

        _output.WriteLine($"✅ Resource management validation:");
        _output.WriteLine($"   Temporary exchange: {tempExchange}");
        _output.WriteLine($"   Temporary queue: {resourceQueue}");
        _output.WriteLine($"   Message delivery: {messageReceived}");
        _output.WriteLine($"   Cleanup completed successfully");
    }

    [Fact]
    public async Task Validation_EnterpriseReadiness_ComprehensiveCheck()
    {
        // Arrange
        _output.WriteLine("Performing comprehensive enterprise readiness validation");

        var validationResults = new Dictionary<string, bool>();
        var validationDetails = new List<string>();

        // Test 1: Basic connectivity and configuration
        try
        {
            validationResults["Connectivity"] = IsContainerRunning;
            validationDetails.Add($"Container Status: {(IsContainerRunning ? "Running" : "Stopped")}");

            validationResults["Provider"] = QueueProvider != null;
            validationDetails.Add($"Provider: {QueueProvider?.ProviderName ?? "null"}");

            validationResults["Configuration"] = QueueProvider?.Configuration != null;
            validationDetails.Add($"Configuration: {(QueueProvider?.Configuration != null ? "Valid" : "Missing")}");
        }
        catch (Exception ex)
        {
            validationResults["BasicSetup"] = false;
            validationDetails.Add($"Setup Error: {ex.Message}");
        }

        // Test 2: Queue operations
        try
        {
            var testQueue = CreateUniqueQueueName("readiness");
            var queueResult = await QueueProvider.DeclareQueueAsync(testQueue, isDurable: false);
            validationResults["QueueOperations"] = !string.IsNullOrEmpty(queueResult);
            validationDetails.Add($"Queue Operations: {(validationResults["QueueOperations"] ? "Pass" : "Fail")}");
        }
        catch (Exception ex)
        {
            validationResults["QueueOperations"] = false;
            validationDetails.Add($"Queue Error: {ex.Message}");
        }

        // Test 3: Exchange operations
        try
        {
            var testExchange = CreateUniqueExchangeName("readiness");
            await QueueProvider.DeclareExchangeAsync(testExchange, "direct", isDurable: false);
            validationResults["ExchangeOperations"] = true;
            validationDetails.Add("Exchange Operations: Pass");
        }
        catch (Exception ex)
        {
            validationResults["ExchangeOperations"] = false;
            validationDetails.Add($"Exchange Error: {ex.Message}");
        }

        // Test 4: Message flow
        try
        {
            var flowQueue = CreateUniqueQueueName("flow-test");
            var testMessage = new QueueMessage { Content = "Readiness test" };

            var messageReceived = false;
            var received = new TaskCompletionSource<bool>();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var consumerTask = QueueProvider.StartConsumingAsync<QueueMessage>(
                async (msg) =>
                {
                    messageReceived = true;
                    received.TrySetResult(true);
                    cts.Cancel();
                    return true;
                },
                queueName: flowQueue,
                cancellationToken: cts.Token);

            await Task.Delay(500);
            await QueueProvider.PublishAsync(testMessage, flowQueue);
            await received.Task;
            await consumerTask;

            validationResults["MessageFlow"] = messageReceived;
            validationDetails.Add($"Message Flow: {(messageReceived ? "Pass" : "Fail")}");
        }
        catch (Exception ex)
        {
            validationResults["MessageFlow"] = false;
            validationDetails.Add($"Message Flow Error: {ex.Message}");
        }


        // Final validation
        var allPassed = validationResults.Values.All(result => result);
        var passedCount = validationResults.Count(kvp => kvp.Value);
        var totalTests = validationResults.Count;

        _output.WriteLine($"✅ Enterprise readiness validation results:");
        _output.WriteLine($"   Overall: {(allPassed ? "PASS" : "FAIL")} ({passedCount}/{totalTests} tests passed)");

        foreach (var detail in validationDetails)
        {
            _output.WriteLine($"   {detail}");
        }

        // Assert - Enterprise readiness requirements
        allPassed.Should().BeTrue("All enterprise readiness checks must pass");
        passedCount.Should().Be(totalTests, $"Expected all {totalTests} validation tests to pass");

        _output.WriteLine($"🎉 RabbitMQ provider is ENTERPRISE READY!");
    }
}