using FluentAssertions;
using OElite;
using OElite.Restme.Abstractions;
using OElite.Restme.ClickHouse.IntegrationTests.Infrastructure;
using OElite.Restme.ClickHouse.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.ClickHouse.IntegrationTests.Tests;

/// <summary>
/// Advanced ClickHouse features integration tests
/// Tests TTL, compression, materialized views, and complex queries
/// </summary>
public class ClickHouseAdvancedFeaturesTests : ClickHouseTestBase
{
    private readonly ITestOutputHelper _output;

    public ClickHouseAdvancedFeaturesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region TTL (Time To Live) Tests

    [Fact]
    public async Task TTL_ShouldCreateAndManageCorrectly_WithDifferentExpressions()
    {
        // Arrange
        var tableName = "ttl_management_test";
        await Rest.CreateTableAsync<LogEntry>(tableName);

        var provider = Rest.GetProvider<IColumnarProvider>();

        // Act & Assert - Test different TTL expressions
        _output.WriteLine("Testing TTL with day-based expiration...");
        await provider.SetTableTTLAsync(tableName, "CreatedAt + INTERVAL 30 DAY");

        _output.WriteLine("Testing TTL with hour-based expiration...");
        await provider.SetTableTTLAsync(tableName, "CreatedAt + INTERVAL 24 HOUR");

        _output.WriteLine("Testing TTL index creation...");
        await provider.CreateTTLIndexAsync(tableName, "CreatedAt", TimeSpan.FromDays(7));

        _output.WriteLine("✅ TTL management working correctly");
    }

    [Fact]
    public async Task TTL_ShouldHandleDataExpiration_WithRealData()
    {
        // Arrange
        var tableName = "ttl_expiration_test";

        // Create table with very short TTL for testing
        var createTableSql = $@"
            CREATE TABLE {tableName} (
                Id UUID,
                Level String,
                Message String,
                CreatedAt DateTime,
                Source Nullable(String)
            )
            ENGINE = MergeTree()
            TTL CreatedAt + INTERVAL 1 SECOND
            ORDER BY CreatedAt
        ";

        await ExecuteSqlAsync(createTableSql);

        // Insert data with past timestamps
        var oldLogEntry = new LogEntry
        {
            Level = "INFO",
            Message = "This should expire",
            CreatedAt = DateTime.UtcNow.AddSeconds(-10), // 10 seconds ago
            Source = "test"
        };

        var recentLogEntry = new LogEntry
        {
            Level = "INFO",
            Message = "This should remain",
            CreatedAt = DateTime.UtcNow,
            Source = "test"
        };

        // Act
        _output.WriteLine("Inserting data with different timestamps...");
        await Rest.InsertAsync(oldLogEntry, tableName);
        await Rest.InsertAsync(recentLogEntry, tableName);

        // Force TTL processing
        await ExecuteSqlAsync($"OPTIMIZE TABLE {tableName} FINAL");
        await Task.Delay(2000);

        var provider = Rest.GetProvider<IColumnarProvider>();
        var remainingCount = await provider!.CountAsync(tableName);

        // Assert - The old entry should be expired
        remainingCount.Should().BeLessThanOrEqualTo(1);
        _output.WriteLine("✅ TTL data expiration working correctly");
    }

    #endregion

    #region Compression Tests

    [Fact]
    public async Task Compression_ShouldWorkCorrectly_WithDifferentCodecs()
    {
        // Arrange & Act
        var lz4TableName = "compression_lz4_test";
        var zstdTableName = "compression_zstd_test";

        _output.WriteLine("Testing LZ4 compression...");
        var lz4TableSql = $@"
            CREATE TABLE {lz4TableName} (
                Id UUID,
                UserId String CODEC(LZ4),
                EventType String CODEC(LZ4),
                Timestamp DateTime,
                EventValue Int32,
                Metadata Nullable(String) CODEC(LZ4)
            )
            ENGINE = MergeTree()
            ORDER BY (Timestamp, UserId)
        ";
        await ExecuteSqlAsync(lz4TableSql);

        _output.WriteLine("Testing ZSTD compression...");
        var zstdTableSql = $@"
            CREATE TABLE {zstdTableName} (
                Id UUID,
                UserId String CODEC(ZSTD),
                EventType String CODEC(ZSTD),
                Timestamp DateTime,
                EventValue Int32,
                Metadata Nullable(String) CODEC(ZSTD)
            )
            ENGINE = MergeTree()
            ORDER BY (Timestamp, UserId)
        ";
        await ExecuteSqlAsync(zstdTableSql);

        // Insert test data
        var events = Enumerable.Range(1, 1000)
            .Select(i => new UserEvent
            {
                UserId = $"user-{i % 10:D3}",
                EventType = "test-event",
                EventValue = i,
                Metadata = $"compressed-metadata-{i}-{'x', 100}" // Repeated content for compression
            })
            .ToList();

        await Rest.BulkInsertAsync(events, lz4TableName);
        await Rest.BulkInsertAsync(events, zstdTableName);
        await Task.Delay(500);

        // Assert
        var provider = Rest.GetProvider<IColumnarProvider>();
        var lz4Count = await provider!.CountAsync(lz4TableName);
        var zstdCount = await provider!.CountAsync(zstdTableName);

        lz4Count.Should().Be(1000);
        zstdCount.Should().Be(1000);

        _output.WriteLine("✅ Compression codecs working correctly");
    }

    #endregion

    #region Materialized Views Tests

    [Fact]
    public async Task MaterializedView_ShouldCreateAndUpdate_WithAggregations()
    {
        // Arrange
        var sourceTableName = "mv_source_events";
        var viewName = "mv_hourly_stats";

        await Rest.CreateTableAsync<UserEvent>(sourceTableName);

        // Create materialized view
        _output.WriteLine("Creating materialized view with hourly aggregations...");
        var createViewSql = $@"
            CREATE MATERIALIZED VIEW {viewName}
            ENGINE = AggregatingMergeTree()
            ORDER BY (hour, EventType)
            AS SELECT
                toStartOfHour(Timestamp) as hour,
                EventType,
                countState() as event_count,
                avgState(EventValue) as avg_value
            FROM {sourceTableName}
            GROUP BY hour, EventType
        ";

        await ExecuteSqlAsync(createViewSql);

        // Insert test data
        var events = Enumerable.Range(1, 100)
            .Select(i => new UserEvent
            {
                UserId = $"user-{i}",
                EventType = i % 2 == 0 ? "click" : "view",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventValue = i
            })
            .ToList();

        // Act
        await Rest.BulkInsertAsync(events, sourceTableName);
        await Task.Delay(1000);

        // Query the materialized view
        var viewResults = await Rest.QueryAsync<dynamic>($@"
            SELECT
                hour,
                EventType,
                countMerge(event_count) as total_events,
                avgMerge(avg_value) as average_value
            FROM {viewName}
            GROUP BY hour, EventType
            ORDER BY hour, EventType
        ");

        // Assert
        viewResults.Should().NotBeNull();
        viewResults.Should().NotBeEmpty();
        _output.WriteLine("✅ Materialized view working correctly");
    }

    #endregion

    #region Complex Query Tests

    [Fact]
    public async Task ComplexQueries_ShouldWorkCorrectly_WithJoinsAndSubqueries()
    {
        // Arrange
        var usersTableName = "users_for_joins";
        var eventsTableName = "events_for_joins";

        // Create users table
        var createUsersTableSql = $@"
            CREATE TABLE {usersTableName} (
                UserId String,
                UserName String,
                SignupDate DateTime
            )
            ENGINE = MergeTree()
            ORDER BY UserId
        ";
        await ExecuteSqlAsync(createUsersTableSql);

        await Rest.CreateTableAsync<UserEvent>(eventsTableName);

        // Insert test data
        var users = new[]
        {
            new { UserId = "user1", UserName = "Alice", SignupDate = DateTime.UtcNow.AddDays(-30) },
            new { UserId = "user2", UserName = "Bob", SignupDate = DateTime.UtcNow.AddDays(-15) },
            new { UserId = "user3", UserName = "Charlie", SignupDate = DateTime.UtcNow.AddDays(-5) }
        };

        foreach (var user in users)
        {
            var insertUserSql = $@"
                INSERT INTO {usersTableName} VALUES
                ('{user.UserId}', '{user.UserName}', '{user.SignupDate:yyyy-MM-dd HH:mm:ss}')
            ";
            await ExecuteSqlAsync(insertUserSql);
        }

        var events = new[]
        {
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 10 },
            new UserEvent { UserId = "user1", EventType = "view", EventValue = 20 },
            new UserEvent { UserId = "user2", EventType = "click", EventValue = 15 },
            new UserEvent { UserId = "user3", EventType = "view", EventValue = 25 }
        };

        await Rest.BulkInsertAsync(events, eventsTableName);
        await Task.Delay(500);

        // Act - Complex query with JOIN and aggregation
        _output.WriteLine("Testing complex queries with JOINs...");
        var complexQueryResults = await Rest.QueryAsync<dynamic>($@"
            SELECT
                u.UserName,
                u.SignupDate,
                count(e.UserId) as total_events,
                avg(e.EventValue) as avg_event_value,
                countIf(e.EventType = 'click') as click_events,
                countIf(e.EventType = 'view') as view_events
            FROM {usersTableName} u
            LEFT JOIN {eventsTableName} e ON u.UserId = e.UserId
            GROUP BY u.UserName, u.SignupDate
            ORDER BY u.UserName
        ");

        // Assert
        complexQueryResults.Should().NotBeNull();
        complexQueryResults.Should().HaveCount(3);
        _output.WriteLine("✅ Complex queries with JOINs working correctly");
    }

    [Fact]
    public async Task WindowFunctions_ShouldWorkCorrectly_WithAnalytics()
    {
        // Arrange
        var tableName = "window_functions_test";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = Enumerable.Range(1, 50)
            .Select(i => new UserEvent
            {
                UserId = $"user-{i % 5 + 1}",
                EventType = "click",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                EventValue = i
            })
            .ToList();

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(300);

        // Act - Test window functions
        _output.WriteLine("Testing window functions...");
        var windowResults = await Rest.QueryAsync<dynamic>($@"
            SELECT
                UserId,
                EventValue,
                row_number() OVER (PARTITION BY UserId ORDER BY EventValue) as row_num,
                rank() OVER (PARTITION BY UserId ORDER BY EventValue) as rank_val,
                lag(EventValue, 1, 0) OVER (PARTITION BY UserId ORDER BY EventValue) as prev_value,
                lead(EventValue, 1, 0) OVER (PARTITION BY UserId ORDER BY EventValue) as next_value
            FROM {tableName}
            ORDER BY UserId, EventValue
            LIMIT 20
        ");

        // Assert
        windowResults.Should().NotBeNull();
        windowResults.Should().NotBeEmpty();
        _output.WriteLine("✅ Window functions working correctly");
    }

    #endregion

    #region Array and Map Operations

    [Fact]
    public async Task ArrayOperations_ShouldWorkCorrectly_WithComplexTypes()
    {
        // Arrange
        var tableName = "array_operations_test";

        var createTableSql = $@"
            CREATE TABLE {tableName} (
                Id UUID,
                Tags Array(String),
                Scores Array(Int32),
                KeyValues Map(String, String),
                Timestamp DateTime
            )
            ENGINE = MergeTree()
            ORDER BY Timestamp
        ";

        await ExecuteSqlAsync(createTableSql);

        // Insert test data with arrays and maps
        var insertSql = $@"
            INSERT INTO {tableName} VALUES
            (generateUUIDv4(), ['tag1', 'tag2', 'tag3'], [10, 20, 30], {{'key1': 'value1', 'key2': 'value2'}}, now()),
            (generateUUIDv4(), ['tag2', 'tag4'], [15, 25], {{'key3': 'value3'}}, now()),
            (generateUUIDv4(), ['tag1', 'tag5'], [5, 35], {{'key1': 'different', 'key4': 'value4'}}, now())
        ";

        await ExecuteSqlAsync(insertSql);
        await Task.Delay(200);

        // Act - Test array operations
        _output.WriteLine("Testing array and map operations...");

        // Test array contains
        var arrayResults = await Rest.QueryAsync<dynamic>($@"
            SELECT
                Id,
                Tags,
                has(Tags, 'tag1') as has_tag1,
                length(Tags) as tag_count,
                arraySum(Scores) as total_score,
                KeyValues['key1'] as key1_value
            FROM {tableName}
            WHERE has(Tags, 'tag1')
        ");

        // Assert
        arrayResults.Should().NotBeNull();
        arrayResults.Should().HaveCount(2);
        _output.WriteLine("✅ Array and map operations working correctly");
    }

    #endregion
}