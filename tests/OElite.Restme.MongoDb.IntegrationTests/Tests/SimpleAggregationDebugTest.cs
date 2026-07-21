using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Simple aggregation debug test to isolate issues step by step
/// </summary>
public class SimpleAggregationDebugTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public SimpleAggregationDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Debug_ShouldTestBasicAggregationStepByStep_WhenUsingSimplePipeline()
    {
        // Arrange - Create minimal test data
        var products = new[]
        {
            new TestProduct { Name = "Laptop", Price = 1000.00m, IsActive = true },
            new TestProduct { Name = "Phone", Price = 500.00m, IsActive = true },
            new TestProduct { Name = "Tablet", Price = 300.00m, IsActive = false }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);
        _output.WriteLine($"Inserted {products.Length} products");

        // First, test simple match only
        var matchOnlyPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } }
        };

        var matchResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(matchOnlyPipeline);
        _output.WriteLine($"Match only returned {matchResults.Count} results");

        if (matchResults.Any())
        {
            var first = matchResults.First();
            _output.WriteLine($"First match result keys: {string.Join(", ", first.Keys)}");
            _output.WriteLine($"First match result price: {first.GetValueOrDefault("price")} (type: {first.GetValueOrDefault("price")?.GetType()})");
            _output.WriteLine($"First match result is_active: {first.GetValueOrDefault("is_active")}");
        }

        // Test simple group without complex operations
        var simpleGroupPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", null },
                    { "count", new Dictionary<string, object> { { "$sum", 1 } } }
                }
            }}
        };

        var groupResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(simpleGroupPipeline);
        _output.WriteLine($"Simple group returned {groupResults.Count} results");

        if (groupResults.Any())
        {
            var result = groupResults.First();
            _output.WriteLine($"Group result keys: {string.Join(", ", result.Keys)}");
            _output.WriteLine($"Group result count: {result.GetValueOrDefault("count")} (type: {result.GetValueOrDefault("count")?.GetType()})");
        }

        // Test price sum specifically
        var priceGroupPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", null },
                    { "total_price", new Dictionary<string, object> { { "$sum", "$price" } } },
                    { "count", new Dictionary<string, object> { { "$sum", 1 } } }
                }
            }}
        };

        var priceResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(priceGroupPipeline);
        _output.WriteLine($"Price group returned {priceResults.Count} results");

        if (priceResults.Any())
        {
            var result = priceResults.First();
            _output.WriteLine($"Price result keys: {string.Join(", ", result.Keys)}");
            _output.WriteLine($"Price result total_price: {result.GetValueOrDefault("total_price")} (type: {result.GetValueOrDefault("total_price")?.GetType()})");
            _output.WriteLine($"Price result count: {result.GetValueOrDefault("count")} (type: {result.GetValueOrDefault("count")?.GetType()})");
        }

        // Assert minimal functionality
        matchResults.Should().HaveCount(2, "because only 2 products are active");
        groupResults.Should().HaveCount(1, "because group should return single result");
        priceResults.Should().HaveCount(1, "because price group should return single result");

        if (priceResults.Any())
        {
            var result = priceResults.First();
            _output.WriteLine($"Final assertion: total_price should be 1500, actual: {result.GetValueOrDefault("total_price")}");
        }
    }
}