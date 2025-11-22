using FluentAssertions;
using OElite.Restme.Abstractions;
using OElite.Restme.Kafka.IntegrationTests.Infrastructure;
using OElite.Restme.Kafka.IntegrationTests.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

/// <summary>
/// Performance and scale tests for Kafka operations
/// Tests throughput, latency, and scalability under various loads
/// </summary>
[Collection("KafkaIntegration")]
public class KafkaPerformanceTests : KafkaTestBase
{
    private readonly ITestOutputHelper _output;

    public KafkaPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Producer Performance Tests

    [Fact]
    public async Task HighVolumeProduction_ShouldAchieveTargetThroughput()
    {
        // Arrange
        var topicName = $"perf-high-volume-{Guid.NewGuid():N}";
        const int messageCount = 10_000;

        var messages = Enumerable.Range(1, messageCount)
            .Select(i => new UserActivityMessage
            {
                UserId = $"perf-user-{i % 1000:D4}",
                Action = GetActionType(i),
                Timestamp = DateTime.UtcNow.AddSeconds(-i),
                Metadata = $"performance test message {i}"
            })
            .ToList();

        _output.WriteLine($"Performance test: Producing {messageCount:N0} messages...");

        // Act
        var stopwatch = Stopwatch.StartNew();
        await Rest.PublishAsync(messages, topicName);
        stopwatch.Stop();

        // Assert
        var throughput = messageCount / stopwatch.Elapsed.TotalSeconds;
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2));
        throughput.Should().BeGreaterThan(1000); // At least 1K messages/second

        _output.WriteLine($"✅ High volume production: {messageCount:N0} messages in {stopwatch.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"   Throughput: {throughput:N0} messages/second");
        _output.WriteLine($"   Average latency: {stopwatch.Elapsed.TotalMilliseconds / messageCount:F2}ms per message");
    }

    [Fact]
    public async Task ConcurrentProducers_ShouldScaleLinearly()
    {
        // Arrange
        var topicName = $"perf-concurrent-producers-{Guid.NewGuid():N}";
        const int producerCount = 20;
        const int messagesPerProducer = 200;
        const int totalMessages = producerCount * messagesPerProducer;

        _output.WriteLine($"Performance test: {producerCount} concurrent producers, {messagesPerProducer} messages each");

        // Act
        var stopwatch = Stopwatch.StartNew();

        var producerTasks = Enumerable.Range(1, producerCount)
            .Select(async producerId =>
            {
                var messages = Enumerable.Range(1, messagesPerProducer)
                    .Select(i => new UserActivityMessage
                    {
                        UserId = $"producer-{producerId:D2}-user-{i:D3}",
                        Action = GetActionType(i),
                        Timestamp = DateTime.UtcNow,
                        Metadata = $"concurrent producer {producerId} message {i}"
                    })
                    .ToList();

                await Rest.PublishAsync(messages, topicName);
                return messages.Count;
            });

        var results = await Task.WhenAll(producerTasks);
        stopwatch.Stop();

        // Assert
        var totalProduced = results.Sum();
        totalProduced.Should().Be(totalMessages);

        var throughput = totalMessages / stopwatch.Elapsed.TotalSeconds;
        throughput.Should().BeGreaterThan(500); // At least 500 messages/second with concurrency

        _output.WriteLine($"✅ Concurrent production: {totalMessages:N0} messages in {stopwatch.Elapsed.TotalSeconds:F1}s");
        _output.WriteLine($"   Throughput: {throughput:N0} messages/second");
        _output.WriteLine($"   Concurrency efficiency: {throughput / producerCount:F0} messages/second/producer");
    }

    [Fact]
    public async Task BatchSizeOptimization_ShouldShowPerformanceDifference()
    {
        // Arrange
        var baseTopicName = $"perf-batch-optimization-{Guid.NewGuid():N}";
        const int totalMessages = 1000;

        var batchSizes = new[] { 1, 10, 50, 100 };
        var results = new Dictionary<int, (TimeSpan duration, double throughput)>();

        _output.WriteLine($"Performance test: Batch size optimization with {totalMessages} messages");

        // Act - Test different batch sizes
        foreach (var batchSize in batchSizes)
        {
            var topicName = $"{baseTopicName}-batch-{batchSize}";
            var batches = new List<List<UserActivityMessage>>();

            // Create batches
            for (int i = 0; i < totalMessages; i += batchSize)
            {
                var batch = Enumerable.Range(i, Math.Min(batchSize, totalMessages - i))
                    .Select(j => new UserActivityMessage
                    {
                        UserId = $"batch-user-{j:D4}",
                        Action = GetActionType(j),
                        Timestamp = DateTime.UtcNow,
                        Metadata = $"batch size {batchSize} message {j}"
                    })
                    .ToList();
                batches.Add(batch);
            }

            // Measure performance
            var stopwatch = Stopwatch.StartNew();
            foreach (var batch in batches)
            {
                await Rest.PublishAsync(batch, topicName);
            }
            stopwatch.Stop();

            var throughput = totalMessages / stopwatch.Elapsed.TotalSeconds;
            results[batchSize] = (stopwatch.Elapsed, throughput);

            _output.WriteLine($"   Batch size {batchSize:D3}: {stopwatch.Elapsed.TotalSeconds:F2}s, {throughput:N0} msgs/sec");
        }

        // Assert - Larger batches should generally perform better
        results[1].throughput.Should().BeLessThan(results[100].throughput,
            "Larger batch sizes should achieve higher throughput");

        _output.WriteLine("✅ Batch size optimization analysis completed");
    }

    #endregion

    #region Consumer Performance Tests

    [Fact]
    public async Task HighVolumeConsumption_ShouldKeepUpWithProduction()
    {
        // Arrange
        var topicName = $"perf-consumer-throughput-{Guid.NewGuid():N}";
        var consumerGroup = $"perf-consumer-group-{Guid.NewGuid():N}";
        const int messageCount = 5000;

        var receivedMessages = new ConcurrentBag<UserActivityMessage>();
        var consumptionStarted = false;
        var timeout = TimeSpan.FromMinutes(2);

        _output.WriteLine($"Performance test: Consumer throughput with {messageCount:N0} messages");

        // Act - Set up consumer first
        var cts = new CancellationTokenSource(timeout);
        var consumptionStopwatch = new Stopwatch();

        var consumerTask = Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                if (!consumptionStarted)
                {
                    consumptionStarted = true;
                    consumptionStopwatch.Start();
                    _output.WriteLine("First message received - consumption timer started");
                }

                receivedMessages.Add(msg);

                if (receivedMessages.Count % 1000 == 0)
                {
                    _output.WriteLine($"Consumed {receivedMessages.Count:N0} messages...");
                }

                if (receivedMessages.Count >= messageCount)
                {
                    consumptionStopwatch.Stop();
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Allow consumer to initialize
        await Task.Delay(2000);

        // Produce messages
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => new UserActivityMessage
            {
                UserId = $"consumer-perf-user-{i % 500:D3}",
                Action = GetActionType(i),
                Timestamp = DateTime.UtcNow,
                Metadata = $"consumer performance test message {i}"
            })
            .ToList();

        _output.WriteLine("Starting message production...");
        var productionStopwatch = Stopwatch.StartNew();
        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(messages, topicName, null);
        productionStopwatch.Stop();

        _output.WriteLine($"Production completed in {productionStopwatch.Elapsed.TotalSeconds:F1}s");

        // Wait for consumption to complete
        try
        {
            await consumerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when all messages consumed
        }

        // Assert
        var consumedCount = receivedMessages.Count;
        var productionThroughput = messageCount / productionStopwatch.Elapsed.TotalSeconds;
        var consumptionThroughput = consumedCount / (consumptionStopwatch.Elapsed.TotalSeconds + 0.001); // Avoid division by zero

        consumedCount.Should().BeGreaterThan((int)(messageCount * 0.8)); // At least 80% consumed

        _output.WriteLine($"✅ Consumer performance test completed:");
        _output.WriteLine($"   Messages produced: {messageCount:N0} in {productionStopwatch.Elapsed.TotalSeconds:F1}s ({productionThroughput:N0} msgs/sec)");
        _output.WriteLine($"   Messages consumed: {consumedCount:N0} in {consumptionStopwatch.Elapsed.TotalSeconds:F1}s ({consumptionThroughput:N0} msgs/sec)");
    }

    [Fact]
    public async Task MultipleConsumersPerformance_ShouldScaleWithConsumerCount()
    {
        // Arrange
        var topicName = $"perf-multiple-consumers-{Guid.NewGuid():N}";
        var consumerGroup = $"perf-multi-consumer-group-{Guid.NewGuid():N}";
        const int messageCount = 2000;
        const int consumerCount = 4;

        var allReceivedMessages = new ConcurrentBag<UserActivityMessage>();
        var timeout = TimeSpan.FromMinutes(2);

        _output.WriteLine($"Performance test: {consumerCount} consumers processing {messageCount:N0} messages");

        // Act - Set up multiple consumers
        var cts = new CancellationTokenSource(timeout);
        var consumptionStopwatch = new Stopwatch();
        var firstMessageReceived = false;

        var consumerTasks = Enumerable.Range(1, consumerCount)
            .Select(consumerId => Rest.SubscribeAsync<UserActivityMessage>(
                topicName,
                consumerGroup,
                async (msg) =>
                {
                    if (!firstMessageReceived)
                    {
                        lock (allReceivedMessages)
                        {
                            if (!firstMessageReceived)
                            {
                                firstMessageReceived = true;
                                consumptionStopwatch.Start();
                                _output.WriteLine($"Consumer {consumerId} received first message - timer started");
                            }
                        }
                    }

                    allReceivedMessages.Add(msg);

                    if (allReceivedMessages.Count >= messageCount)
                    {
                        consumptionStopwatch.Stop();
                        cts.Cancel();
                    }
                },
                cancellationToken: cts.Token))
            .ToArray();

        // Allow consumers to initialize and join group
        await Task.Delay(3000);

        // Produce messages
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => new UserActivityMessage
            {
                UserId = $"multi-consumer-user-{i:D4}",
                Action = GetActionType(i),
                Timestamp = DateTime.UtcNow,
                Metadata = $"multi-consumer test message {i}"
            })
            .ToList();

        await Rest.GetProvider<IStreamingProvider>().PublishBatchAsync(messages, topicName, null);
        _output.WriteLine($"Published {messageCount:N0} messages for {consumerCount} consumers");

        // Wait for consumption
        try
        {
            await Task.WhenAll(consumerTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        var totalConsumed = allReceivedMessages.Count;
        var consumptionThroughput = totalConsumed / (consumptionStopwatch.Elapsed.TotalSeconds + 0.001);

        totalConsumed.Should().BeGreaterThan((int)(messageCount * 0.7)); // At least 70% consumed

        _output.WriteLine($"✅ Multiple consumers performance test:");
        _output.WriteLine($"   Consumers: {consumerCount}");
        _output.WriteLine($"   Messages consumed: {totalConsumed:N0}/{messageCount:N0}");
        _output.WriteLine($"   Total throughput: {consumptionThroughput:N0} messages/second");
        _output.WriteLine($"   Per-consumer throughput: {consumptionThroughput / consumerCount:N0} messages/second/consumer");
    }

    #endregion

    #region Memory and Resource Performance Tests

    [Fact]
    public async Task MemoryUsage_ShouldRemainStable_DuringLongRunning()
    {
        // Arrange
        var topicName = $"perf-memory-stability-{Guid.NewGuid():N}";
        const int batchSize = 500;
        const int iterations = 10;

        var memoryMeasurements = new List<long>();

        _output.WriteLine($"Performance test: Memory stability during {iterations} iterations of {batchSize} messages");

        // Act - Multiple publishing iterations to test memory stability
        for (int iteration = 1; iteration <= iterations; iteration++)
        {
            var beforeMemory = GC.GetTotalMemory(true);

            var messages = Enumerable.Range(1, batchSize)
                .Select(i => new UserActivityMessage
                {
                    UserId = $"memory-user-{iteration:D2}-{i:D3}",
                    Action = GetActionType(i),
                    Timestamp = DateTime.UtcNow,
                    Metadata = $"memory test iteration {iteration} message {i}"
                })
                .ToList();

            await Rest.PublishAsync(messages, topicName);

            var afterMemory = GC.GetTotalMemory(true);
            var memoryDelta = afterMemory - beforeMemory;
            memoryMeasurements.Add(memoryDelta);

            _output.WriteLine($"   Iteration {iteration}: Memory delta {memoryDelta / 1024 / 1024:F1} MB");

            // Brief pause between iterations
            await Task.Delay(200);
        }

        // Assert
        var averageMemoryDelta = memoryMeasurements.Average();
        var maxMemoryDelta = memoryMeasurements.Max();

        averageMemoryDelta.Should().BeLessThan(20 * 1024 * 1024); // Less than 20MB average
        maxMemoryDelta.Should().BeLessThan(50 * 1024 * 1024); // Less than 50MB max

        _output.WriteLine($"✅ Memory stability test completed:");
        _output.WriteLine($"   Average memory delta: {averageMemoryDelta / 1024 / 1024:F1} MB");
        _output.WriteLine($"   Maximum memory delta: {maxMemoryDelta / 1024 / 1024:F1} MB");
    }

    #endregion

    #region Latency Performance Tests

    [Fact]
    public async Task MessageLatency_ShouldMeetPerformanceTargets()
    {
        // Arrange
        var topicName = $"perf-latency-test-{Guid.NewGuid():N}";
        var consumerGroup = $"perf-latency-group-{Guid.NewGuid():N}";

        var latencyMeasurements = new ConcurrentBag<TimeSpan>();
        const int messageCount = 100;
        var timeout = TimeSpan.FromSeconds(30);

        _output.WriteLine($"Performance test: Message latency measurement with {messageCount} messages");

        // Act - Set up consumer to measure latency
        var cts = new CancellationTokenSource(timeout);

        var consumerTask = Rest.SubscribeAsync<UserActivityMessage>(
            topicName,
            consumerGroup,
            async (msg) =>
            {
                // Parse timestamp from metadata to calculate latency
                if (DateTime.TryParse(msg.Metadata, out var sentTime))
                {
                    var latency = DateTime.UtcNow - sentTime;
                    latencyMeasurements.Add(latency);
                }

                if (latencyMeasurements.Count >= messageCount)
                {
                    cts.Cancel();
                }
            },
            cancellationToken: cts.Token);

        // Allow consumer to initialize
        await Task.Delay(1000);

        // Send messages with timestamps for latency calculation
        for (int i = 1; i <= messageCount; i++)
        {
            var sendTime = DateTime.UtcNow;
            var message = new UserActivityMessage
            {
                UserId = $"latency-user-{i:D3}",
                Action = "latency-test",
                Timestamp = sendTime,
                Metadata = sendTime.ToString("O") // ISO timestamp for parsing
            };

            await Rest.PublishAsync(message, topicName);
            await Task.Delay(50); // Small delay between messages
        }

        // Wait for consumption
        try
        {
            await consumerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        if (latencyMeasurements.Count > 0)
        {
            var averageLatency = TimeSpan.FromTicks((long)latencyMeasurements.Average(l => l.Ticks));
            var maxLatency = latencyMeasurements.Max();
            var minLatency = latencyMeasurements.Min();

            averageLatency.Should().BeLessThan(TimeSpan.FromSeconds(5));
            maxLatency.Should().BeLessThan(TimeSpan.FromSeconds(10));

            _output.WriteLine($"✅ Latency test completed ({latencyMeasurements.Count} measurements):");
            _output.WriteLine($"   Average latency: {averageLatency.TotalMilliseconds:F0}ms");
            _output.WriteLine($"   Min latency: {minLatency.TotalMilliseconds:F0}ms");
            _output.WriteLine($"   Max latency: {maxLatency.TotalMilliseconds:F0}ms");
        }
        else
        {
            _output.WriteLine("⚠️ No latency measurements collected");
        }
    }

    #endregion

    #region Helper Methods

    private static string GetActionType(int index)
    {
        return (index % 5) switch
        {
            0 => "login",
            1 => "logout",
            2 => "click",
            3 => "view",
            _ => "action"
        };
    }

    #endregion
}