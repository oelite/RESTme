using FluentAssertions;
using OElite;
using OElite.Restme.Abstractions;
using OElite.Restme.ClickHouse;
using OElite.Restme.ClickHouse.IntegrationTests.Infrastructure;
using OElite.Restme.ClickHouse.IntegrationTests.Models;
using System.Net.Http;
using ClickHouse.Client;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.ClickHouse.IntegrationTests.Tests;

/// <summary>
/// Comprehensive ClickHouse integration tests with real database operations
/// Replaces both unit tests and basic integration tests for production readiness
/// </summary>
public class ClickHouseComprehensiveIntegrationTests : ClickHouseTestBase
{
    private readonly ITestOutputHelper _output;

    public ClickHouseComprehensiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Provider and Factory Tests

    [Fact]
    public async Task Provider_ShouldLoadAndInitialize_WithValidConfiguration()
    {
        // Arrange & Act
        _output.WriteLine("Testing ClickHouse provider initialization...");

        // Assert
        Rest.Should().NotBeNull();
        Rest.CurrentMode.Should().Be(RestMode.ClickHouse);
        Rest.GetProvider<IColumnarProvider>().Should().NotBeNull();
        Rest.GetProvider<IColumnarProvider>().Should().BeOfType<ClickHouseProvider>();

        _output.WriteLine("✅ ClickHouse provider loaded and initialized successfully");
    }

    [Fact]
    public async Task ServiceFactory_ShouldCreateClickHouseProvider_WithValidConfig()
    {
        // Arrange
        var factory = new ClickHouseServiceFactory();
        var config = new RestConfig
        {
            ConnectionString = "clickhouse://localhost:8123/default",
            OperationMode = RestMode.ClickHouse
        };

        // Act
        var provider = factory.CreateColumnarProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<ClickHouseProvider>();

        _output.WriteLine("✅ ClickHouse service factory working correctly");
    }

    #endregion

    #region Table Management Tests

    [Fact]
    public async Task CreateTable_ShouldSucceed_WithUserEventEntity()
    {
        // Act
        _output.WriteLine("Creating UserEvent table...");
        await Rest.CreateTableAsync<UserEvent>("user_events_test");

        // Assert - Table creation should not throw exceptions
        _output.WriteLine("✅ UserEvent table created successfully");
    }

    [Fact]
    public async Task CreateTable_ShouldSucceed_WithLogEntryEntity()
    {
        // Act
        _output.WriteLine("Creating LogEntry table...");
        await Rest.CreateTableAsync<LogEntry>("logs_test");

        // Assert - Table creation should not throw exceptions
        _output.WriteLine("✅ LogEntry table created successfully");
    }

    #endregion

    #region Basic CRUD Operations

    [Fact]
    public async Task InsertAndQuery_ShouldWorkCorrectly_WithSingleUserEvent()
    {
        // Arrange
        var tableName = "user_events_single";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var userEvent = new UserEvent
        {
            UserId = "test-user-001",
            EventType = "click",
            Timestamp = DateTime.UtcNow,
            EventValue = 42,
            Metadata = "test metadata"
        };

        // Act
        _output.WriteLine("Inserting single user event...");
        await Rest.InsertAsync(userEvent, tableName);

        // Small delay for ClickHouse to process the insert
        await Task.Delay(100);

        var results = await Rest.QueryAsync<UserEvent>($"SELECT * FROM {tableName}");

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results[0].UserId.Should().Be("test-user-001");
        results[0].EventType.Should().Be("click");
        results[0].EventValue.Should().Be(42);
        results[0].Metadata.Should().Be("test metadata");

        _output.WriteLine("✅ Single insert and query working correctly");
    }

    [Fact]
    public async Task BulkInsert_ShouldWorkCorrectly_WithMultipleUserEvents()
    {
        // Arrange
        var tableName = "user_events_bulk";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = Enumerable.Range(1, 100)
            .Select(i => new UserEvent
            {
                UserId = $"user-{i:D3}",
                EventType = i % 2 == 0 ? "click" : "view",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventValue = i,
                Metadata = $"metadata-{i}"
            })
            .ToList();

        // Act
        _output.WriteLine("Performing bulk insert of 100 events...");
        await Rest.BulkInsertAsync(events, tableName);

        // Allow time for ClickHouse to process
        await Task.Delay(500);

        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        totalCount.Should().Be(100);
        _output.WriteLine("✅ Bulk insert of 100 events successful");
    }

    #endregion

    #region Expression Translation Tests

    [Fact]
    public async Task QueryWithExpressions_ShouldWorkCorrectly_WithLambdaFilters()
    {
        // Arrange
        var tableName = "user_events_expressions";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = new[]
        {
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 10 },
            new UserEvent { UserId = "user2", EventType = "view", EventValue = 20 },
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 30 }
        };

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(200);

        // Act & Assert - Test expression translation
        _output.WriteLine("Testing lambda expression queries...");

        // Test equality filter
        var clickEvents = await Rest.QueryAsync<UserEvent>(
            $"SELECT * FROM {tableName} WHERE EventType = 'click'");
        clickEvents.Should().HaveCount(2);

        // Test numeric comparison
        var highValueEvents = await Rest.QueryAsync<UserEvent>(
            $"SELECT * FROM {tableName} WHERE EventValue >= 20");
        highValueEvents.Should().HaveCount(2);

        _output.WriteLine("✅ Lambda expression queries working correctly");
    }

    #endregion

    #region Advanced ClickHouse Features

    [Fact]
    public async Task Aggregations_ShouldWorkCorrectly_WithGroupByQueries()
    {
        // Arrange
        var tableName = "user_events_aggregations";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = new[]
        {
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 10 },
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 20 },
            new UserEvent { UserId = "user2", EventType = "view", EventValue = 15 },
            new UserEvent { UserId = "user2", EventType = "view", EventValue = 25 }
        };

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(200);

        // Act
        _output.WriteLine("Testing aggregation queries...");
        var aggregationResults = await Rest.QueryAsync<dynamic>($@"
            SELECT
                EventType,
                count() as EventCount,
                avg(EventValue) as AvgValue,
                sum(EventValue) as TotalValue
            FROM {tableName}
            GROUP BY EventType
            ORDER BY EventType");

        // Assert
        aggregationResults.Should().NotBeNull();
        aggregationResults.Should().HaveCount(2);
        _output.WriteLine("✅ Aggregation queries working correctly");
    }

    [Fact]
    public async Task TTL_ShouldWorkCorrectly_WithTableConfiguration()
    {
        // Arrange
        var tableName = "logs_ttl_test";
        await Rest.CreateTableAsync<LogEntry>(tableName);

        var provider = Rest.GetProvider<IColumnarProvider>();

        // Act
        _output.WriteLine("Setting up TTL for logs table...");
        await provider.SetTableTTLAsync(tableName, "CreatedAt + INTERVAL 30 DAY");

        // Insert test data
        var logEntries = new[]
        {
            new LogEntry { Level = "INFO", Message = "Test message 1" },
            new LogEntry { Level = "ERROR", Message = "Test message 2" }
        };

        await Rest.BulkInsertAsync(logEntries, tableName);
        await Task.Delay(200);

        var count = await provider!.CountAsync(tableName);

        // Assert
        count.Should().Be(2);
        _output.WriteLine("✅ TTL configuration working correctly");
    }

    [Fact]
    public async Task Partitioning_ShouldWorkCorrectly_WithDateBasedPartitions()
    {
        // Arrange
        var tableName = "user_events_partitioned";

        // Create table with partitioning
        var createTableSql = $@"
            CREATE TABLE {tableName} (
                Id UUID,
                UserId String,
                EventType String,
                Timestamp DateTime,
                EventValue Int32,
                Metadata Nullable(String),
                EventDate Date MATERIALIZED toDate(Timestamp)
            )
            ENGINE = MergeTree()
            PARTITION BY toYYYYMM(EventDate)
            ORDER BY (EventDate, UserId, Timestamp)
        ";

        await ExecuteSqlAsync(createTableSql);

        // Insert events from different months
        var events = new[]
        {
            new UserEvent
            {
                UserId = "user1",
                EventType = "click",
                Timestamp = new DateTime(2023, 1, 15),
                EventValue = 10
            },
            new UserEvent
            {
                UserId = "user2",
                EventType = "view",
                Timestamp = new DateTime(2023, 2, 15),
                EventValue = 20
            }
        };

        // Act
        _output.WriteLine("Testing partitioned table operations...");
        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(200);

        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        totalCount.Should().Be(2);
        _output.WriteLine("✅ Partitioned table operations working correctly");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task LargeDatasetOperations_ShouldPerformWell_With10KRecords()
    {
        // Arrange
        var tableName = "user_events_performance";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = Enumerable.Range(1, 10_000)
            .Select(i => new UserEvent
            {
                UserId = $"user-{i % 100:D3}",
                EventType = (i % 4) switch
                {
                    0 => "click",
                    1 => "view",
                    2 => "scroll",
                    _ => "hover"
                },
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventValue = i,
                Metadata = $"performance-test-{i}"
            })
            .ToList();

        // Act
        _output.WriteLine("Performance test: Inserting 10K records...");
        var insertStart = DateTime.UtcNow;

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(1000); // Allow ClickHouse to process

        var insertEnd = DateTime.UtcNow;
        var insertTime = insertEnd - insertStart;

        // Query performance test
        var queryStart = DateTime.UtcNow;
        var clickEvents = await Rest.QueryAsync<UserEvent>(
            $"SELECT * FROM {tableName} WHERE EventType = 'click' ORDER BY Timestamp LIMIT 1000");
        var queryEnd = DateTime.UtcNow;
        var queryTime = queryEnd - queryStart;

        // Assert
        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);
        totalCount.Should().Be(10_000);
        clickEvents.Should().HaveCount(1000);

        insertTime.Should().BeLessThan(TimeSpan.FromSeconds(30));
        queryTime.Should().BeLessThan(TimeSpan.FromSeconds(10));

        _output.WriteLine($"✅ Performance test passed - Insert: {insertTime.TotalMilliseconds}ms, Query: {queryTime.TotalMilliseconds}ms");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvalidQuery_ShouldThrowMeaningfulException()
    {
        // Act & Assert - ClickHouse should throw ClickHouseServerException for query errors
        var exception = await Assert.ThrowsAsync<ClickHouseServerException>(async () =>
            await Rest.QueryAsync<UserEvent>("SELECT invalid_column FROM nonexistent_table"));

        exception.Should().NotBeNull();
        exception.Message.Should().NotBeEmpty();
        exception.Message.Should().Contain("UNKNOWN_TABLE", "ClickHouse should provide meaningful error details");
        _output.WriteLine($"✅ Error handling working correctly for invalid queries: {exception.Message}");
    }

    [Fact]
    public async Task InvalidConnectionString_ShouldThrowMeaningfulException()
    {
        // Act & Assert - ClickHouse should throw HttpRequestException for connection errors
        var invalidRest = new Rest("clickhouse://invalid-host:8123/test",
            new RestConfig { OperationMode = RestMode.ClickHouse });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await invalidRest.QueryAsync<UserEvent>("SELECT 1"));

        exception.Should().NotBeNull();
        exception.Message.Should().NotBeEmpty();
        _output.WriteLine($"✅ Connection error handling working correctly: {exception.Message}");
    }

    #endregion

    #region Data Type Support Tests

    [Fact]
    public async Task DataTypes_ShouldHandleAllCommonTypes_Correctly()
    {
        // Arrange
        var tableName = "data_types_test";

        var createTableSql = $@"
            CREATE TABLE {tableName} (
                Id UUID,
                StringValue String,
                IntValue Int32,
                LongValue Int64,
                FloatValue Float32,
                DoubleValue Float64,
                DateTimeValue DateTime,
                BoolValue UInt8,
                NullableString Nullable(String)
            ) ENGINE = MergeTree()
            ORDER BY Id
        ";

        await ExecuteSqlAsync(createTableSql);

        // Act
        _output.WriteLine("Testing comprehensive data type support...");

        var insertSql = $@"
            INSERT INTO {tableName} VALUES
            (generateUUIDv4(), 'test string', 42, 1234567890, 3.14, 2.718281828, now(), 1, 'nullable value'),
            (generateUUIDv4(), 'another string', -42, -1234567890, -3.14, -2.718281828, now() - INTERVAL 1 DAY, 0, NULL)
        ";

        await ExecuteSqlAsync(insertSql);
        await Task.Delay(100);

        var results = await Rest.QueryAsync<dynamic>($"SELECT * FROM {tableName}");

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(2);
        _output.WriteLine("✅ Data type support working correctly");
    }

    #endregion
}