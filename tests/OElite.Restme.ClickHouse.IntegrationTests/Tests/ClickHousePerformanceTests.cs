using FluentAssertions;
using OElite;
using OElite.Restme.Abstractions;
using OElite.Restme.ClickHouse.IntegrationTests.Infrastructure;
using OElite.Restme.ClickHouse.IntegrationTests.Models;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.ClickHouse.IntegrationTests.Tests;

/// <summary>
/// Performance and scale tests for ClickHouse operations
/// Tests throughput, latency, and scalability under various loads
/// </summary>
public class ClickHousePerformanceTests : ClickHouseTestBase
{
    private readonly ITestOutputHelper _output;

    public ClickHousePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Bulk Insert Performance Tests

    [Fact]
    public async Task BulkInsert_ShouldHandleLargeDatasets_Within10Seconds()
    {
        // Arrange
        var tableName = "performance_bulk_insert";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        const int recordCount = 100_000;
        var events = Enumerable.Range(1, recordCount)
            .Select(i => new UserEvent
            {
                UserId = $"perf-user-{i % 1000:D4}",
                EventType = GetEventType(i),
                Timestamp = DateTime.UtcNow.AddSeconds(-i),
                EventValue = i,
                Metadata = GenerateMetadata(i)
            })
            .ToList();

        // Act
        _output.WriteLine($"Performance test: Bulk inserting {recordCount:N0} records...");
        var stopwatch = Stopwatch.StartNew();

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(2000); // Allow ClickHouse to process

        stopwatch.Stop();

        // Verify data was inserted
        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        totalCount.Should().Be(recordCount);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        var throughput = recordCount / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"✅ Bulk insert performance: {recordCount:N0} records in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"   Throughput: {throughput:N0} records/second");

        // Performance benchmarks
        throughput.Should().BeGreaterThan(10_000); // At least 10K records/second
    }

    [Fact]
    public async Task ConcurrentInserts_ShouldHandleMultipleConnections_Efficiently()
    {
        // Arrange
        var tableName = "performance_concurrent_inserts";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        const int batchSize = 5_000;
        const int concurrentBatches = 10;

        var allBatches = Enumerable.Range(0, concurrentBatches)
            .Select(batchIndex => Enumerable.Range(1, batchSize)
                .Select(i => new UserEvent
                {
                    UserId = $"concurrent-user-{batchIndex}-{i:D4}",
                    EventType = GetEventType(i),
                    Timestamp = DateTime.UtcNow.AddSeconds(-i),
                    EventValue = i + (batchIndex * batchSize),
                    Metadata = $"batch-{batchIndex}-metadata-{i}"
                })
                .ToList())
            .ToList();

        // Act
        _output.WriteLine($"Performance test: {concurrentBatches} concurrent batches of {batchSize:N0} records each...");
        var stopwatch = Stopwatch.StartNew();

        var insertTasks = allBatches.Select(batch => Rest.BulkInsertAsync(batch, tableName));
        await Task.WhenAll(insertTasks);
        await Task.Delay(3000); // Allow ClickHouse to process all batches

        stopwatch.Stop();

        // Verify data was inserted
        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        var expectedTotal = batchSize * concurrentBatches;
        totalCount.Should().Be(expectedTotal);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));

        var throughput = expectedTotal / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"✅ Concurrent insert performance: {expectedTotal:N0} records in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"   Throughput: {throughput:N0} records/second");
    }

    #endregion

    #region Query Performance Tests

    [Fact]
    public async Task AggregationQueries_ShouldPerformWell_OnLargeDatasets()
    {
        // Arrange
        var tableName = "performance_aggregation_queries";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        const int recordCount = 50_000;
        var events = Enumerable.Range(1, recordCount)
            .Select(i => new UserEvent
            {
                UserId = $"agg-user-{i % 500:D3}",
                EventType = GetEventType(i),
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventValue = i,
                Metadata = GenerateMetadata(i)
            })
            .ToList();

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(2000);

        // Act - Test different aggregation queries
        var queries = new Dictionary<string, string>
        {
            ["Simple Count"] = $"SELECT count() FROM {tableName}",
            ["Group By User"] = $"SELECT UserId, count(), avg(EventValue) FROM {tableName} GROUP BY UserId ORDER BY count() DESC LIMIT 10",
            ["Group By Event Type"] = $"SELECT EventType, count(), sum(EventValue), min(Timestamp), max(Timestamp) FROM {tableName} GROUP BY EventType",
            ["Complex Aggregation"] = $@"
                SELECT
                    UserId,
                    EventType,
                    count() as event_count,
                    avg(EventValue) as avg_value,
                    quantile(0.95)(EventValue) as p95_value
                FROM {tableName}
                WHERE Timestamp >= now() - INTERVAL 1 DAY
                GROUP BY UserId, EventType
                HAVING event_count > 1
                ORDER BY avg_value DESC
                LIMIT 50"
        };

        _output.WriteLine("Performance test: Running aggregation queries on large dataset...");
        var results = new Dictionary<string, (TimeSpan duration, int resultCount)>();

        foreach (var (queryName, sql) in queries)
        {
            var stopwatch = Stopwatch.StartNew();
            var queryResults = await Rest.QueryAsync<dynamic>(sql);
            stopwatch.Stop();

            results[queryName] = (stopwatch.Elapsed, queryResults.Count);
            _output.WriteLine($"   {queryName}: {stopwatch.Elapsed.TotalMilliseconds:F0}ms, {queryResults.Count} results");
        }

        // Assert - All queries should complete within reasonable time
        foreach (var (queryName, (duration, resultCount)) in results)
        {
            duration.Should().BeLessThan(TimeSpan.FromSeconds(5),
                $"Query '{queryName}' took too long: {duration.TotalMilliseconds}ms");
            resultCount.Should().BeGreaterThan(0, $"Query '{queryName}' returned no results");
        }

        _output.WriteLine("✅ Aggregation query performance acceptable");
    }

    [Fact]
    public async Task FilteredQueries_ShouldPerformWell_WithIndexes()
    {
        // Arrange
        var tableName = "performance_filtered_queries";

        // Create table with optimized indexes
        var createTableSql = $@"
            CREATE TABLE {tableName} (
                Id UUID,
                UserId String,
                EventType String,
                Timestamp DateTime,
                EventValue Int32,
                Metadata Nullable(String),
                INDEX idx_user_type (UserId, EventType) TYPE bloom_filter GRANULARITY 1,
                INDEX idx_timestamp (Timestamp) TYPE minmax GRANULARITY 1,
                INDEX idx_value (EventValue) TYPE minmax GRANULARITY 1
            )
            ENGINE = MergeTree()
            ORDER BY (Timestamp, UserId)
        ";

        await ExecuteSqlAsync(createTableSql);

        const int recordCount = 100_000;
        var events = Enumerable.Range(1, recordCount)
            .Select(i => new UserEvent
            {
                UserId = $"filter-user-{i % 1000:D3}",
                EventType = GetEventType(i),
                Timestamp = DateTime.UtcNow.AddMinutes(-i % 10080), // Spread across a week
                EventValue = i,
                Metadata = GenerateMetadata(i)
            })
            .ToList();

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(3000);

        // Act - Test filtered queries with different selectivity
        var filterQueries = new Dictionary<string, string>
        {
            ["High Selectivity"] = $"SELECT * FROM {tableName} WHERE UserId = 'filter-user-001' ORDER BY Timestamp",
            ["Medium Selectivity"] = $"SELECT * FROM {tableName} WHERE EventType = 'click' AND EventValue > 50000 ORDER BY Timestamp LIMIT 1000",
            ["Low Selectivity"] = $"SELECT * FROM {tableName} WHERE EventValue BETWEEN 10000 AND 90000 ORDER BY Timestamp LIMIT 5000",
            ["Time Range"] = $"SELECT * FROM {tableName} WHERE Timestamp >= now() - INTERVAL 1 HOUR ORDER BY Timestamp",
            ["Complex Filter"] = $"SELECT * FROM {tableName} WHERE UserId LIKE 'filter-user-1%' AND EventType IN ('click', 'view') AND EventValue > 1000 ORDER BY Timestamp LIMIT 1000"
        };

        _output.WriteLine("Performance test: Running filtered queries with indexes...");
        var results = new Dictionary<string, (TimeSpan duration, int resultCount)>();

        foreach (var (queryName, sql) in filterQueries)
        {
            var stopwatch = Stopwatch.StartNew();
            var queryResults = await Rest.QueryAsync<dynamic>(sql);
            stopwatch.Stop();

            results[queryName] = (stopwatch.Elapsed, queryResults.Count);
            _output.WriteLine($"   {queryName}: {stopwatch.Elapsed.TotalMilliseconds:F0}ms, {queryResults.Count} results");
        }

        // Assert - All queries should complete quickly with proper indexes
        foreach (var (queryName, (duration, resultCount)) in results)
        {
            duration.Should().BeLessThan(TimeSpan.FromSeconds(2),
                $"Filtered query '{queryName}' took too long: {duration.TotalMilliseconds}ms");
        }

        _output.WriteLine("✅ Filtered query performance with indexes acceptable");
    }

    #endregion

    #region Memory and Resource Tests

    [Fact]
    public async Task MemoryUsage_ShouldRemainStable_DuringLongRunningOperations()
    {
        // Arrange
        var tableName = "performance_memory_test";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        const int batchSize = 10_000;
        const int iterations = 10;

        _output.WriteLine($"Performance test: Memory stability during {iterations} iterations of {batchSize:N0} records...");

        // Act - Multiple iterations to test memory stability
        var memoryMeasurements = new List<long>();

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var events = Enumerable.Range(1, batchSize)
                .Select(i => new UserEvent
                {
                    UserId = $"memory-user-{iteration}-{i:D4}",
                    EventType = GetEventType(i),
                    Timestamp = DateTime.UtcNow.AddSeconds(-i),
                    EventValue = i + (iteration * batchSize),
                    Metadata = GenerateMetadata(i)
                })
                .ToList();

            var beforeMemory = GC.GetTotalMemory(true);

            await Rest.BulkInsertAsync(events, tableName);

            var afterMemory = GC.GetTotalMemory(true);
            memoryMeasurements.Add(afterMemory - beforeMemory);

            _output.WriteLine($"   Iteration {iteration + 1}: Memory delta {(afterMemory - beforeMemory) / 1024 / 1024:F1} MB");

            // Small delay between iterations
            await Task.Delay(100);
        }

        // Verify total count
        await Task.Delay(2000);
        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        totalCount.Should().Be(batchSize * iterations);

        // Memory usage should not grow excessively
        var avgMemoryDelta = memoryMeasurements.Average();
        var maxMemoryDelta = memoryMeasurements.Max();

        avgMemoryDelta.Should().BeLessThan(50 * 1024 * 1024); // Less than 50MB average
        maxMemoryDelta.Should().BeLessThan(100 * 1024 * 1024); // Less than 100MB max

        _output.WriteLine($"✅ Memory stability test passed - Avg: {avgMemoryDelta / 1024 / 1024:F1} MB, Max: {maxMemoryDelta / 1024 / 1024:F1} MB");
    }

    #endregion

    #region Connection Pool Tests

    [Fact]
    public async Task ConnectionPool_ShouldHandleConcurrentRequests_Efficiently()
    {
        // Arrange
        var tableName = "performance_connection_pool";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        // Insert some test data
        var setupEvents = Enumerable.Range(1, 1000)
            .Select(i => new UserEvent
            {
                UserId = $"pool-user-{i % 50:D2}",
                EventType = GetEventType(i),
                EventValue = i
            })
            .ToList();

        await Rest.BulkInsertAsync(setupEvents, tableName);
        await Task.Delay(1000);

        const int concurrentQueries = 50;
        const int queriesPerTask = 10;

        _output.WriteLine($"Performance test: {concurrentQueries} concurrent query tasks, {queriesPerTask} queries each...");

        // Act - Run concurrent queries to test connection pooling
        var stopwatch = Stopwatch.StartNew();

        var queryTasks = Enumerable.Range(0, concurrentQueries)
            .Select(async taskId =>
            {
                var results = new List<dynamic>();
                for (int queryId = 0; queryId < queriesPerTask; queryId++)
                {
                    var sql = $@"
                        SELECT UserId, count(), avg(EventValue)
                        FROM {tableName}
                        WHERE EventValue > {taskId * 10}
                        GROUP BY UserId
                        LIMIT 10
                    ";
                    var queryResult = await Rest.QueryAsync<dynamic>(sql);
                    results.AddRange(queryResult);
                }
                return results.Count;
            })
            .ToList();

        var allResults = await Task.WhenAll(queryTasks);
        stopwatch.Stop();

        // Assert
        var totalQueries = concurrentQueries * queriesPerTask;
        allResults.Should().HaveCount(concurrentQueries);
        allResults.Should().AllSatisfy(result => result.Should().BeGreaterThan(0));

        var avgLatency = stopwatch.Elapsed.TotalMilliseconds / totalQueries;
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));

        _output.WriteLine($"✅ Connection pool test passed - {totalQueries} queries in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        _output.WriteLine($"   Average latency: {avgLatency:F1}ms per query");
    }

    #endregion

    #region Helper Methods

    private static string GetEventType(int index)
    {
        return (index % 4) switch
        {
            0 => "click",
            1 => "view",
            2 => "scroll",
            _ => "hover"
        };
    }

    private static string GenerateMetadata(int index)
    {
        var categories = new[] { "electronics", "books", "clothing", "sports", "music" };
        var actions = new[] { "browse", "purchase", "wishlist", "compare", "review" };

        return $"{{\"category\": \"{categories[index % categories.Length]}\", \"action\": \"{actions[index % actions.Length]}\", \"session_id\": \"{Guid.NewGuid()}\", \"index\": {index}}}";
    }

    #endregion
}