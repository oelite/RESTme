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
/// Basic ClickHouse integration tests with real database operations
/// Tests core functionality to ensure production readiness
/// </summary>
public class ClickHouseBasicIntegrationTests : ClickHouseTestBase
{
    private readonly ITestOutputHelper _output;

    public ClickHouseBasicIntegrationTests(ITestOutputHelper output)
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
        var config = new RestConfig(RestMode.ClickHouse)
        {
            ConnectionString = "clickhouse://localhost:8123/default"
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
        await Rest.CreateTableAsync<UserEvent>("user_events_basic_test");

        // Assert - Table creation should not throw exceptions
        _output.WriteLine("✅ UserEvent table created successfully");
    }

    #endregion

    #region Basic CRUD Operations

    [Fact]
    public async Task InsertAndQuery_ShouldWorkCorrectly_WithSingleUserEvent()
    {
        // Arrange
        var tableName = "user_events_single_basic";
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
        var tableName = "user_events_bulk_basic";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = Enumerable.Range(1, 50)
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
        _output.WriteLine("Performing bulk insert of 50 events...");
        await Rest.BulkInsertAsync(events, tableName);

        // Allow time for ClickHouse to process
        await Task.Delay(500);

        var provider = Rest.GetProvider<IColumnarProvider>();
        var totalCount = await provider!.CountAsync(tableName);

        // Assert
        totalCount.Should().Be(50);
        _output.WriteLine("✅ Bulk insert of 50 events successful");
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task QueryWithFilters_ShouldWorkCorrectly_WithBasicConditions()
    {
        // Arrange
        var tableName = "user_events_filter_basic";
        await Rest.CreateTableAsync<UserEvent>(tableName);

        var events = new[]
        {
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 10 },
            new UserEvent { UserId = "user2", EventType = "view", EventValue = 20 },
            new UserEvent { UserId = "user1", EventType = "click", EventValue = 30 }
        };

        await Rest.BulkInsertAsync(events, tableName);
        await Task.Delay(200);

        // Act & Assert - Test basic queries
        _output.WriteLine("Testing basic filter queries...");

        // Test equality filter
        var clickEvents = await Rest.QueryAsync<UserEvent>(
            $"SELECT * FROM {tableName} WHERE EventType = 'click'");
        clickEvents.Should().HaveCount(2);

        // Test numeric comparison
        var highValueEvents = await Rest.QueryAsync<UserEvent>(
            $"SELECT * FROM {tableName} WHERE EventValue >= 20");
        highValueEvents.Should().HaveCount(2);

        _output.WriteLine("✅ Basic filter queries working correctly");
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

    #endregion
}