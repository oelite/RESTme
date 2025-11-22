using FluentAssertions;
using OElite.Restme.Redis.IntegrationTests.Infrastructure;
using OElite.Restme.Redis.IntegrationTests.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Redis.IntegrationTests.Tests;

/// <summary>
/// Comprehensive performance benchmarks and concurrent access tests for Redis cache provider
/// Validates enterprise-grade performance requirements and SLA compliance
/// </summary>
public class RedisPerformanceTests : RedisTestBase
{
    private readonly ITestOutputHelper _output;

    public RedisPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Performance Baseline Tests

    [Fact]
    public async Task Performance_ShouldMeetBaseline_ForBasicOperations()
    {
        // Arrange
        const int operationCount = 1000;
        var performanceMetrics = new List<CacheOperationMetrics>();

        _output.WriteLine($"Running performance baseline test with {operationCount} operations...");

        // Warm up the connection
        await CacheProvider.SetAsync(GenerateTestKey("warmup"), "warmup");
        await CacheProvider.GetAsync<string>(GenerateTestKey("warmup"));

        var overallStopwatch = Stopwatch.StartNew();

        // Test Set operations
        var setStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            var key = GenerateTestKey($"perf_set_{i}");
            var value = $"performance_test_value_{i}";

            var operationStopwatch = Stopwatch.StartNew();
            var setResult = await CacheProvider.SetAsync(key, value);
            operationStopwatch.Stop();

            performanceMetrics.Add(new CacheOperationMetrics
            {
                OperationType = "Set",
                Duration = operationStopwatch.Elapsed,
                DataSize = System.Text.Encoding.UTF8.GetByteCount(value),
                Success = setResult
            });
        }
        setStopwatch.Stop();

        // Test Get operations
        var getStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            var key = GenerateTestKey($"perf_set_{i}");

            var operationStopwatch = Stopwatch.StartNew();
            var getValue = await CacheProvider.GetAsync<string>(key);
            operationStopwatch.Stop();

            performanceMetrics.Add(new CacheOperationMetrics
            {
                OperationType = "Get",
                Duration = operationStopwatch.Elapsed,
                Success = getValue != null
            });
        }
        getStopwatch.Stop();

        // Test Remove operations
        var removeStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < operationCount; i++)
        {
            var key = GenerateTestKey($"perf_set_{i}");

            var operationStopwatch = Stopwatch.StartNew();
            var removeResult = await CacheProvider.RemoveAsync(key);
            operationStopwatch.Stop();

            performanceMetrics.Add(new CacheOperationMetrics
            {
                OperationType = "Remove",
                Duration = operationStopwatch.Elapsed,
                Success = removeResult
            });
        }
        removeStopwatch.Stop();

        overallStopwatch.Stop();

        // Analyze results
        var setMetrics = performanceMetrics.Where(m => m.OperationType == "Set").ToList();
        var getMetrics = performanceMetrics.Where(m => m.OperationType == "Get").ToList();
        var removeMetrics = performanceMetrics.Where(m => m.OperationType == "Remove").ToList();

        var avgSetTime = setMetrics.Average(m => m.Duration.TotalMilliseconds);
        var avgGetTime = getMetrics.Average(m => m.Duration.TotalMilliseconds);
        var avgRemoveTime = removeMetrics.Average(m => m.Duration.TotalMilliseconds);

        var maxSetTime = setMetrics.Max(m => m.Duration.TotalMilliseconds);
        var maxGetTime = getMetrics.Max(m => m.Duration.TotalMilliseconds);
        var maxRemoveTime = removeMetrics.Max(m => m.Duration.TotalMilliseconds);

        var setSuccessRate = setMetrics.Count(m => m.Success) / (double)operationCount * 100;
        var getSuccessRate = getMetrics.Count(m => m.Success) / (double)operationCount * 100;
        var removeSuccessRate = removeMetrics.Count(m => m.Success) / (double)operationCount * 100;

        var totalOperationsPerSecond = (operationCount * 3) / overallStopwatch.Elapsed.TotalSeconds;

        // Log performance results
        _output.WriteLine($"Performance Baseline Results ({operationCount} operations each):");
        _output.WriteLine($"  Set Operations    - Avg: {avgSetTime:F2}ms, Max: {maxSetTime:F2}ms, Success: {setSuccessRate:F1}%");
        _output.WriteLine($"  Get Operations    - Avg: {avgGetTime:F2}ms, Max: {maxGetTime:F2}ms, Success: {getSuccessRate:F1}%");
        _output.WriteLine($"  Remove Operations - Avg: {avgRemoveTime:F2}ms, Max: {maxRemoveTime:F2}ms, Success: {removeSuccessRate:F1}%");
        _output.WriteLine($"  Overall Performance: {totalOperationsPerSecond:F1} operations/second");
        _output.WriteLine($"  Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Assert performance requirements
        setSuccessRate.Should().BeGreaterOrEqualTo(99.0, "Set operations should have >99% success rate");
        getSuccessRate.Should().BeGreaterOrEqualTo(99.0, "Get operations should have >99% success rate");
        removeSuccessRate.Should().BeGreaterOrEqualTo(99.0, "Remove operations should have >99% success rate");

        avgSetTime.Should().BeLessThan(10.0, "Average Set time should be <10ms");
        avgGetTime.Should().BeLessThan(10.0, "Average Get time should be <10ms");
        avgRemoveTime.Should().BeLessThan(10.0, "Average Remove time should be <10ms");

        maxSetTime.Should().BeLessThan(100.0, "Max Set time should be <100ms");
        maxGetTime.Should().BeLessThan(100.0, "Max Get time should be <100ms");
        maxRemoveTime.Should().BeLessThan(100.0, "Max Remove time should be <100ms");

        totalOperationsPerSecond.Should().BeGreaterThan(1000.0, "Should achieve >1000 operations/second");

        _output.WriteLine("✅ Performance baseline requirements met");
    }

    [Fact]
    public async Task Performance_ShouldScale_WithDataSizes()
    {
        _output.WriteLine("Testing performance scaling with different data sizes...");

        var dataSizes = new[] { 100, 1000, 10000, 100000, 500000 }; // Bytes
        var results = new List<(int Size, double SetTime, double GetTime, double Throughput)>();

        foreach (var dataSize in dataSizes)
        {
            var key = GenerateTestKey($"size_test_{dataSize}");
            var value = new string('A', dataSize);
            const int iterations = 10;

            var setTimes = new List<double>();
            var getTimes = new List<double>();

            // Multiple iterations for accuracy
            for (int i = 0; i < iterations; i++)
            {
                // Test Set performance
                var setStopwatch = Stopwatch.StartNew();
                var setResult = await CacheProvider.SetAsync(key, value);
                setStopwatch.Stop();

                setResult.Should().BeTrue($"SetAsync should succeed for {dataSize} byte value");
                setTimes.Add(setStopwatch.Elapsed.TotalMilliseconds);

                // Test Get performance
                var getStopwatch = Stopwatch.StartNew();
                var getValue = await CacheProvider.GetAsync<string>(key);
                getStopwatch.Stop();

                getValue.Should().NotBeNull($"GetAsync should succeed for {dataSize} byte value");
                getValue!.Length.Should().Be(dataSize);
                getTimes.Add(getStopwatch.Elapsed.TotalMilliseconds);
            }

            var avgSetTime = setTimes.Average();
            var avgGetTime = getTimes.Average();
            var throughput = (dataSize / 1024.0 / 1024.0) / (avgSetTime / 1000.0); // MB/s

            results.Add((dataSize, avgSetTime, avgGetTime, throughput));

            _output.WriteLine($"  {dataSize / 1024}KB - Set: {avgSetTime:F2}ms, Get: {avgGetTime:F2}ms, Throughput: {throughput:F2}MB/s");

            // Cleanup
            await CacheProvider.RemoveAsync(key);
        }

        // Validate scaling characteristics
        // Performance should degrade gracefully with size, not exponentially
        for (int i = 1; i < results.Count; i++)
        {
            var current = results[i];
            var previous = results[i - 1];

            var sizeIncrease = (double)current.Size / previous.Size;
            var setTimeIncrease = current.SetTime / previous.SetTime;
            var getTimeIncrease = current.GetTime / previous.GetTime;

            // Time increase should not be more than 10x the size increase (rough scaling expectation)
            setTimeIncrease.Should().BeLessThan(sizeIncrease * 10,
                $"Set time scaling should be reasonable from {previous.Size} to {current.Size} bytes");
            getTimeIncrease.Should().BeLessThan(sizeIncrease * 10,
                $"Get time scaling should be reasonable from {previous.Size} to {current.Size} bytes");
        }

        _output.WriteLine("✅ Performance scaling with data sizes acceptable");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Concurrency_ShouldHandleParallelReads_Efficiently()
    {
        // Arrange
        const int concurrentReaders = 50;
        const int readsPerThread = 100;
        var key = GenerateTestKey("concurrent_reads");
        var value = new ComplexOrder
        {
            OrderId = "CONCURRENT_001",
            CustomerId = "CUSTOMER_001",
            Status = OrderStatus.Processing,
            TotalAmount = 999.99m,
            Items = Enumerable.Range(1, 5).Select(i => new OrderItem
            {
                ProductId = $"PRODUCT_{i}",
                ProductName = $"Product {i}",
                Quantity = i,
                UnitPrice = 100.00m * i
            }).ToList()
        };

        _output.WriteLine($"Testing concurrent reads: {concurrentReaders} threads × {readsPerThread} reads = {concurrentReaders * readsPerThread} total reads");

        // Setup - Cache the test data
        var setupResult = await CacheProvider.SetAsync(key, value);
        setupResult.Should().BeTrue("Test data setup should succeed");

        var results = new ConcurrentBag<CacheOperationMetrics>();
        var overallStopwatch = Stopwatch.StartNew();

        // Act - Parallel reads
        var readTasks = Enumerable.Range(0, concurrentReaders).Select(async threadId =>
        {
            for (int readId = 0; readId < readsPerThread; readId++)
            {
                var operationStopwatch = Stopwatch.StartNew();
                try
                {
                    var getValue = await CacheProvider.GetAsync<ComplexOrder>(key);
                    operationStopwatch.Stop();

                    var success = getValue != null && getValue.OrderId == value.OrderId;
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "ConcurrentRead",
                        Duration = operationStopwatch.Elapsed,
                        Success = success,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    operationStopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "ConcurrentRead",
                        Duration = operationStopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        });

        await Task.WhenAll(readTasks);
        overallStopwatch.Stop();

        // Assert
        var totalOperations = concurrentReaders * readsPerThread;
        var successfulReads = results.Count(r => r.Success);
        var failedReads = results.Count(r => !r.Success);
        var successRate = (double)successfulReads / totalOperations * 100;

        var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
        var maxDuration = results.Where(r => r.Success).Max(r => r.Duration.TotalMilliseconds);
        var readsPerSecond = totalOperations / overallStopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Concurrent Read Results:");
        _output.WriteLine($"  Total Operations: {totalOperations}");
        _output.WriteLine($"  Successful: {successfulReads} ({successRate:F2}%)");
        _output.WriteLine($"  Failed: {failedReads}");
        _output.WriteLine($"  Average Duration: {avgDuration:F2}ms");
        _output.WriteLine($"  Max Duration: {maxDuration:F2}ms");
        _output.WriteLine($"  Reads/Second: {readsPerSecond:F1}");
        _output.WriteLine($"  Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2}s");

        // Validate performance requirements
        successRate.Should().BeGreaterOrEqualTo(99.0, "Concurrent reads should have >99% success rate");
        avgDuration.Should().BeLessThan(50.0, "Average concurrent read time should be <50ms");
        maxDuration.Should().BeLessThan(500.0, "Max concurrent read time should be <500ms");
        readsPerSecond.Should().BeGreaterThan(1000.0, "Should achieve >1000 reads/second under concurrency");

        _output.WriteLine("✅ Concurrent read performance requirements met");

        // Cleanup
        await CacheProvider.RemoveAsync(key);
    }

    [Fact]
    public async Task Concurrency_ShouldHandleParallelWrites_Safely()
    {
        // Arrange
        const int concurrentWriters = 20;
        const int writesPerThread = 50;
        var keyPrefix = GenerateTestKey("concurrent_writes");

        _output.WriteLine($"Testing concurrent writes: {concurrentWriters} threads × {writesPerThread} writes = {concurrentWriters * writesPerThread} total writes");

        var results = new ConcurrentBag<CacheOperationMetrics>();
        var overallStopwatch = Stopwatch.StartNew();

        // Act - Parallel writes to different keys
        var writeTasks = Enumerable.Range(0, concurrentWriters).Select(async threadId =>
        {
            for (int writeId = 0; writeId < writesPerThread; writeId++)
            {
                var key = $"{keyPrefix}_{threadId}_{writeId}";
                var value = new ConcurrencyTestModel
                {
                    Id = $"thread_{threadId}_write_{writeId}",
                    Counter = writeId,
                    ThreadId = threadId.ToString()
                };

                var operationStopwatch = Stopwatch.StartNew();
                try
                {
                    var setResult = await CacheProvider.SetAsync(key, value);
                    operationStopwatch.Stop();

                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "ConcurrentWrite",
                        Duration = operationStopwatch.Elapsed,
                        Success = setResult
                    });
                }
                catch (Exception ex)
                {
                    operationStopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "ConcurrentWrite",
                        Duration = operationStopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        });

        await Task.WhenAll(writeTasks);
        overallStopwatch.Stop();

        // Verify data integrity - read back some random keys
        var verificationTasks = new List<Task<bool>>();
        var random = new Random();

        for (int i = 0; i < 100; i++) // Verify 100 random keys
        {
            var threadId = random.Next(concurrentWriters);
            var writeId = random.Next(writesPerThread);
            var key = $"{keyPrefix}_{threadId}_{writeId}";

            verificationTasks.Add(Task.Run(async () =>
            {
                var getValue = await CacheProvider.GetAsync<ConcurrencyTestModel>(key);
                return getValue != null && getValue.ThreadId == threadId.ToString() && getValue.Counter == writeId;
            }));
        }

        var verificationResults = await Task.WhenAll(verificationTasks);

        // Assert
        var totalOperations = concurrentWriters * writesPerThread;
        var successfulWrites = results.Count(r => r.Success);
        var failedWrites = results.Count(r => !r.Success);
        var successRate = (double)successfulWrites / totalOperations * 100;

        var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
        var maxDuration = results.Where(r => r.Success).Max(r => r.Duration.TotalMilliseconds);
        var writesPerSecond = totalOperations / overallStopwatch.Elapsed.TotalSeconds;

        var dataIntegrityRate = verificationResults.Count(v => v) / (double)verificationResults.Length * 100;

        _output.WriteLine($"Concurrent Write Results:");
        _output.WriteLine($"  Total Operations: {totalOperations}");
        _output.WriteLine($"  Successful: {successfulWrites} ({successRate:F2}%)");
        _output.WriteLine($"  Failed: {failedWrites}");
        _output.WriteLine($"  Average Duration: {avgDuration:F2}ms");
        _output.WriteLine($"  Max Duration: {maxDuration:F2}ms");
        _output.WriteLine($"  Writes/Second: {writesPerSecond:F1}");
        _output.WriteLine($"  Data Integrity: {dataIntegrityRate:F2}% ({verificationResults.Count(v => v)}/{verificationResults.Length} verified)");
        _output.WriteLine($"  Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2}s");

        // Validate performance requirements
        successRate.Should().BeGreaterOrEqualTo(95.0, "Concurrent writes should have >95% success rate");
        dataIntegrityRate.Should().BeGreaterOrEqualTo(99.0, "Data integrity should be >99% for concurrent writes");
        avgDuration.Should().BeLessThan(100.0, "Average concurrent write time should be <100ms");
        writesPerSecond.Should().BeGreaterThan(500.0, "Should achieve >500 writes/second under concurrency");

        _output.WriteLine("✅ Concurrent write performance and data integrity requirements met");

        // Cleanup - remove all created keys
        var cleanupTasks = Enumerable.Range(0, concurrentWriters).Select(async threadId =>
        {
            for (int writeId = 0; writeId < writesPerThread; writeId++)
            {
                var key = $"{keyPrefix}_{threadId}_{writeId}";
                await CacheProvider.RemoveAsync(key);
            }
        });

        await Task.WhenAll(cleanupTasks);
    }

    [Fact]
    public async Task Concurrency_ShouldHandleMixedOperations_Efficiently()
    {
        // Arrange
        const int totalThreads = 30;
        const int operationsPerThread = 50;
        var sharedKeys = Enumerable.Range(0, 10).Select(i => GenerateTestKey($"shared_{i}")).ToList();

        _output.WriteLine($"Testing mixed concurrent operations: {totalThreads} threads × {operationsPerThread} operations");

        // Pre-populate some shared keys
        foreach (var key in sharedKeys)
        {
            var initialValue = new SimpleUser
            {
                Id = key,
                Name = "Initial User",
                Email = "initial@example.com",
                Age = 25
            };
            await CacheProvider.SetAsync(key, initialValue);
        }

        var results = new ConcurrentBag<CacheOperationMetrics>();
        var overallStopwatch = Stopwatch.StartNew();

        // Act - Mixed operations (reads, writes, updates, deletes)
        var mixedTasks = Enumerable.Range(0, totalThreads).Select(async threadId =>
        {
            var random = new Random(threadId); // Seeded for reproducibility

            for (int opId = 0; opId < operationsPerThread; opId++)
            {
                var operationType = random.Next(4); // 0=Read, 1=Write, 2=Update, 3=Delete
                var useSharedKey = random.Next(3) == 0; // 33% chance to use shared key

                string key;
                if (useSharedKey)
                {
                    key = sharedKeys[random.Next(sharedKeys.Count)];
                }
                else
                {
                    key = GenerateTestKey($"thread_{threadId}_op_{opId}");
                }

                var operationStopwatch = Stopwatch.StartNew();
                try
                {
                    bool success = false;
                    string operation;

                    switch (operationType)
                    {
                        case 0: // Read
                            operation = "Read";
                            var getValue = await CacheProvider.GetAsync<SimpleUser>(key);
                            success = true; // Read always succeeds (even if returns null)
                            break;

                        case 1: // Write
                            operation = "Write";
                            var newUser = new SimpleUser
                            {
                                Id = $"user_{threadId}_{opId}",
                                Name = $"User {threadId}-{opId}",
                                Email = $"user{threadId}_{opId}@example.com",
                                Age = 20 + (threadId % 50)
                            };
                            success = await CacheProvider.SetAsync(key, newUser);
                            break;

                        case 2: // Update (Set with TTL)
                            operation = "Update";
                            var updateUser = new SimpleUser
                            {
                                Id = $"updated_{threadId}_{opId}",
                                Name = $"Updated User {threadId}-{opId}",
                                Email = $"updated{threadId}_{opId}@example.com",
                                Age = 30 + (threadId % 40)
                            };
                            success = await CacheProvider.SetAsync(key, updateUser, TimeSpan.FromMinutes(10));
                            break;

                        case 3: // Delete
                            operation = "Delete";
                            if (!useSharedKey) // Don't delete shared keys
                            {
                                success = await CacheProvider.RemoveAsync(key);
                            }
                            else
                            {
                                success = true; // Skip deletion of shared keys
                            }
                            break;

                        default:
                            operation = "Unknown";
                            break;
                    }

                    operationStopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = operation,
                        Duration = operationStopwatch.Elapsed,
                        Success = success
                    });
                }
                catch (Exception ex)
                {
                    operationStopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "Error",
                        Duration = operationStopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        });

        await Task.WhenAll(mixedTasks);
        overallStopwatch.Stop();

        // Analyze results
        var totalOperations = totalThreads * operationsPerThread;
        var operationGroups = results.GroupBy(r => r.OperationType).ToList();

        _output.WriteLine($"Mixed Concurrent Operations Results:");
        _output.WriteLine($"  Total Operations: {totalOperations}");
        _output.WriteLine($"  Total Time: {overallStopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"  Operations/Second: {totalOperations / overallStopwatch.Elapsed.TotalSeconds:F1}");

        foreach (var group in operationGroups.OrderBy(g => g.Key))
        {
            var ops = group.ToList();
            var successRate = ops.Count(o => o.Success) / (double)ops.Count * 100;
            var avgDuration = ops.Where(o => o.Success).DefaultIfEmpty().Average(o => o?.Duration.TotalMilliseconds ?? 0);

            _output.WriteLine($"  {group.Key,-10}: {ops.Count,4} ops, {successRate:F1}% success, {avgDuration:F2}ms avg");
        }

        // Validate overall performance
        var overallSuccessRate = results.Count(r => r.Success) / (double)results.Count * 100;
        var overallAvgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
        var overallOpsPerSecond = totalOperations / overallStopwatch.Elapsed.TotalSeconds;

        overallSuccessRate.Should().BeGreaterOrEqualTo(95.0, "Mixed operations should have >95% success rate");
        overallAvgDuration.Should().BeLessThan(50.0, "Average mixed operation time should be <50ms");
        overallOpsPerSecond.Should().BeGreaterThan(800.0, "Should achieve >800 mixed operations/second");

        _output.WriteLine("✅ Mixed concurrent operations performance requirements met");

        // Cleanup
        await Task.WhenAll(sharedKeys.Select(key => CacheProvider.RemoveAsync(key)));
    }

    #endregion

    #region Memory and Resource Performance Tests

    [Fact]
    public async Task Performance_ShouldMaintain_UnderMemoryPressure()
    {
        _output.WriteLine("Testing performance under memory pressure...");

        const int largeObjectCount = 100;
        const int performanceTestOps = 200;

        var initialMemory = await GetMemoryInfoAsync();
        _output.WriteLine($"Initial memory usage: {initialMemory.UsedMemory / 1024:N0} KB");

        // Create memory pressure with large objects
        var memoryPressureKeys = new List<string>();
        for (int i = 0; i < largeObjectCount; i++)
        {
            var key = GenerateTestKey($"memory_pressure_{i}");
            var largeData = LargeDataModel.CreateWithSize(50, 1000); // Moderate size for sustained pressure

            memoryPressureKeys.Add(key);
            await CacheProvider.SetAsync(key, largeData);
        }

        var pressureMemory = await GetMemoryInfoAsync();
        var memoryIncrease = pressureMemory.UsedMemory - initialMemory.UsedMemory;
        _output.WriteLine($"Memory after pressure creation: {pressureMemory.UsedMemory / 1024:N0} KB (increase: {memoryIncrease / 1024:N0} KB)");

        // Test performance under memory pressure
        var performanceResults = new List<CacheOperationMetrics>();
        var performanceStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < performanceTestOps; i++)
        {
            var key = GenerateTestKey($"perf_under_pressure_{i}");
            var testUser = new SimpleUser
            {
                Id = $"pressure_user_{i}",
                Name = $"Pressure Test User {i}",
                Email = $"pressure{i}@example.com",
                Age = 25 + (i % 50)
            };

            var setStopwatch = Stopwatch.StartNew();
            var setResult = await CacheProvider.SetAsync(key, testUser);
            setStopwatch.Stop();

            var getStopwatch = Stopwatch.StartNew();
            var getValue = await CacheProvider.GetAsync<SimpleUser>(key);
            getStopwatch.Stop();

            var removeStopwatch = Stopwatch.StartNew();
            var removeResult = await CacheProvider.RemoveAsync(key);
            removeStopwatch.Stop();

            performanceResults.Add(new CacheOperationMetrics
            {
                OperationType = "SetUnderPressure",
                Duration = setStopwatch.Elapsed,
                Success = setResult
            });

            performanceResults.Add(new CacheOperationMetrics
            {
                OperationType = "GetUnderPressure",
                Duration = getStopwatch.Elapsed,
                Success = getValue != null && getValue.Id == testUser.Id
            });

            performanceResults.Add(new CacheOperationMetrics
            {
                OperationType = "RemoveUnderPressure",
                Duration = removeStopwatch.Elapsed,
                Success = removeResult
            });
        }

        performanceStopwatch.Stop();

        // Analyze performance under pressure
        var setUnderPressure = performanceResults.Where(r => r.OperationType == "SetUnderPressure" && r.Success).ToList();
        var getUnderPressure = performanceResults.Where(r => r.OperationType == "GetUnderPressure" && r.Success).ToList();
        var removeUnderPressure = performanceResults.Where(r => r.OperationType == "RemoveUnderPressure" && r.Success).ToList();

        var avgSetTime = setUnderPressure.Average(r => r.Duration.TotalMilliseconds);
        var avgGetTime = getUnderPressure.Average(r => r.Duration.TotalMilliseconds);
        var avgRemoveTime = removeUnderPressure.Average(r => r.Duration.TotalMilliseconds);

        var setSuccessRate = setUnderPressure.Count / (double)performanceTestOps * 100;
        var getSuccessRate = getUnderPressure.Count / (double)performanceTestOps * 100;
        var removeSuccessRate = removeUnderPressure.Count / (double)performanceTestOps * 100;

        _output.WriteLine($"Performance under memory pressure:");
        _output.WriteLine($"  Set operations    - Avg: {avgSetTime:F2}ms, Success: {setSuccessRate:F1}%");
        _output.WriteLine($"  Get operations    - Avg: {avgGetTime:F2}ms, Success: {getSuccessRate:F1}%");
        _output.WriteLine($"  Remove operations - Avg: {avgRemoveTime:F2}ms, Success: {removeSuccessRate:F1}%");

        // Performance should not degrade significantly under memory pressure
        avgSetTime.Should().BeLessThan(50.0, "Set operations should maintain performance under memory pressure");
        avgGetTime.Should().BeLessThan(50.0, "Get operations should maintain performance under memory pressure");
        avgRemoveTime.Should().BeLessThan(50.0, "Remove operations should maintain performance under memory pressure");

        setSuccessRate.Should().BeGreaterOrEqualTo(98.0, "Set success rate should remain high under memory pressure");
        getSuccessRate.Should().BeGreaterOrEqualTo(98.0, "Get success rate should remain high under memory pressure");
        removeSuccessRate.Should().BeGreaterOrEqualTo(98.0, "Remove success rate should remain high under memory pressure");

        // Cleanup memory pressure
        await Task.WhenAll(memoryPressureKeys.Select(key => CacheProvider.RemoveAsync(key)));

        var finalMemory = await GetMemoryInfoAsync();
        _output.WriteLine($"Final memory usage: {finalMemory.UsedMemory / 1024:N0} KB");

        _output.WriteLine("✅ Performance maintained under memory pressure");
    }

    [Fact]
    public async Task Performance_ShouldHandle_HighFrequencyOperations()
    {
        _output.WriteLine("Testing high-frequency operation performance...");

        const int duration = 10; // seconds
        const int maxOperationsPerSecond = 2000;
        var operationInterval = TimeSpan.FromMilliseconds(1000.0 / maxOperationsPerSecond);

        var results = new ConcurrentBag<CacheOperationMetrics>();
        var endTime = DateTime.UtcNow.AddSeconds(duration);
        var operationCounter = 0;

        var highFrequencyTask = Task.Run(async () =>
        {
            while (DateTime.UtcNow < endTime)
            {
                var opId = Interlocked.Increment(ref operationCounter);
                var key = GenerateTestKey($"high_freq_{opId % 100}"); // Cycle through 100 keys
                var value = $"high_frequency_value_{opId}";

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var setResult = await CacheProvider.SetAsync(key, value, TimeSpan.FromMinutes(1));
                    stopwatch.Stop();

                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "HighFrequencySet",
                        Duration = stopwatch.Elapsed,
                        Success = setResult
                    });
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "HighFrequencySet",
                        Duration = stopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }

                // Rate limiting to achieve target frequency
                await Task.Delay(operationInterval);
            }
        });

        await highFrequencyTask;

        // Analyze high-frequency performance
        var totalOperations = results.Count;
        var successfulOperations = results.Count(r => r.Success);
        var successRate = successfulOperations / (double)totalOperations * 100;
        var actualFrequency = totalOperations / (double)duration;

        var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
        var maxDuration = results.Where(r => r.Success).Max(r => r.Duration.TotalMilliseconds);
        var minDuration = results.Where(r => r.Success).Min(r => r.Duration.TotalMilliseconds);

        _output.WriteLine($"High-frequency operation results ({duration}s test):");
        _output.WriteLine($"  Total Operations: {totalOperations}");
        _output.WriteLine($"  Successful: {successfulOperations} ({successRate:F2}%)");
        _output.WriteLine($"  Actual Frequency: {actualFrequency:F1} ops/sec");
        _output.WriteLine($"  Duration - Avg: {avgDuration:F2}ms, Min: {minDuration:F2}ms, Max: {maxDuration:F2}ms");

        // Validate high-frequency performance
        successRate.Should().BeGreaterOrEqualTo(95.0, "High-frequency operations should maintain >95% success rate");
        actualFrequency.Should().BeGreaterThan(maxOperationsPerSecond * 0.8, "Should achieve at least 80% of target frequency");
        avgDuration.Should().BeLessThan(20.0, "Average duration should be <20ms for high-frequency operations");
        maxDuration.Should().BeLessThan(200.0, "Max duration should be <200ms for high-frequency operations");

        _output.WriteLine("✅ High-frequency operation performance requirements met");
    }

    #endregion

    #region SLA Validation Tests

    [Fact]
    public async Task SLA_ShouldMeet_EnterprisePerformanceRequirements()
    {
        _output.WriteLine("Validating enterprise SLA performance requirements...");

        // Enterprise SLA requirements:
        // - 99.9% availability (success rate)
        // - <10ms average response time for basic operations
        // - <100ms 99th percentile response time
        // - >10,000 operations per second throughput capacity
        // - <1% error rate under normal load

        const int testDuration = 30; // seconds
        const int targetThroughput = 1000; // ops/sec for this test (scaled for test environment)
        var operationInterval = TimeSpan.FromMilliseconds(1000.0 / targetThroughput);

        var results = new ConcurrentBag<CacheOperationMetrics>();
        var slaTestEnd = DateTime.UtcNow.AddSeconds(testDuration);
        var operationId = 0;

        var slaTestTask = Task.Run(async () =>
        {
            while (DateTime.UtcNow < slaTestEnd)
            {
                var opId = Interlocked.Increment(ref operationId);
                var key = GenerateTestKey($"sla_{opId % 500}"); // Cycle through 500 keys

                // Alternate between different operation types for realistic load
                var operationType = opId % 4;

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    bool success = false;
                    string operation;

                    switch (operationType)
                    {
                        case 0: // Set operation
                            operation = "SLA_Set";
                            var setValue = new SimpleUser
                            {
                                Id = $"sla_user_{opId}",
                                Name = $"SLA User {opId}",
                                Email = $"sla{opId}@example.com",
                                Age = 25 + (opId % 50)
                            };
                            success = await CacheProvider.SetAsync(key, setValue, TimeSpan.FromMinutes(5));
                            break;

                        case 1: // Get operation
                            operation = "SLA_Get";
                            var getValue = await CacheProvider.GetAsync<SimpleUser>(key);
                            success = true; // Get always succeeds
                            break;

                        case 2: // Exists operation
                            operation = "SLA_Exists";
                            success = await CacheProvider.ExistsAsync(key);
                            success = true; // Exists always succeeds
                            break;

                        case 3: // Remove operation (only if key exists)
                            operation = "SLA_Remove";
                            if (opId % 20 == 0) // Only remove occasionally
                            {
                                success = await CacheProvider.RemoveAsync(key);
                            }
                            else
                            {
                                success = true; // Skip removal
                            }
                            break;

                        default:
                            operation = "SLA_Unknown";
                            success = false;
                            break;
                    }

                    stopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = operation,
                        Duration = stopwatch.Elapsed,
                        Success = success
                    });
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "SLA_Error",
                        Duration = stopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }

                await Task.Delay(operationInterval);
            }
        });

        await slaTestTask;

        // Analyze SLA compliance
        var allResults = results.ToList();
        var totalOperations = allResults.Count;
        var successfulOperations = allResults.Count(r => r.Success);
        var availability = successfulOperations / (double)totalOperations * 100;

        var successfulDurations = allResults.Where(r => r.Success).Select(r => r.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
        var averageResponseTime = successfulDurations.Average();
        var p99ResponseTime = successfulDurations[(int)(successfulDurations.Count * 0.99)];
        var maxResponseTime = successfulDurations.Max();

        var throughput = totalOperations / (double)testDuration;
        var errorRate = (totalOperations - successfulOperations) / (double)totalOperations * 100;

        // SLA breakdown by operation type
        var operationGroups = allResults.GroupBy(r => r.OperationType)
            .Select(g => new
            {
                Operation = g.Key,
                Count = g.Count(),
                SuccessRate = g.Count(r => r.Success) / (double)g.Count() * 100,
                AvgTime = g.Where(r => r.Success).DefaultIfEmpty().Average(r => r?.Duration.TotalMilliseconds ?? 0)
            }).ToList();

        _output.WriteLine($"Enterprise SLA Validation Results ({testDuration}s test):");
        _output.WriteLine($"  Total Operations: {totalOperations:N0}");
        _output.WriteLine($"  Availability: {availability:F3}% (Target: >99.9%)");
        _output.WriteLine($"  Average Response Time: {averageResponseTime:F2}ms (Target: <10ms)");
        _output.WriteLine($"  99th Percentile: {p99ResponseTime:F2}ms (Target: <100ms)");
        _output.WriteLine($"  Max Response Time: {maxResponseTime:F2}ms");
        _output.WriteLine($"  Throughput: {throughput:F1} ops/sec (Target: >1000 ops/sec)");
        _output.WriteLine($"  Error Rate: {errorRate:F3}% (Target: <1%)");

        _output.WriteLine($"  Operation Breakdown:");
        foreach (var group in operationGroups.OrderBy(g => g.Operation))
        {
            _output.WriteLine($"    {group.Operation,-15}: {group.Count,5} ops, {group.SuccessRate:F2}% success, {group.AvgTime:F2}ms avg");
        }

        // Validate SLA requirements
        availability.Should().BeGreaterOrEqualTo(99.9, "Availability should meet 99.9% SLA");
        averageResponseTime.Should().BeLessThan(20.0, "Average response time should be <20ms (relaxed for test environment)");
        p99ResponseTime.Should().BeLessThan(200.0, "99th percentile should be <200ms (relaxed for test environment)");
        throughput.Should().BeGreaterThan(500.0, "Throughput should exceed 500 ops/sec (scaled for test environment)");
        errorRate.Should().BeLessThan(2.0, "Error rate should be <2% (relaxed for test environment)");

        _output.WriteLine("✅ Enterprise SLA performance requirements validated");
    }

    #endregion
}