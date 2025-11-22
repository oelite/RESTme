using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Validates that the Pipeline method generates correct MongoDB aggregation pipelines
/// This ensures the fix actually creates proper MongoDB queries, not just passes tests
/// </summary>
public class PipelineValidationTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public PipelineValidationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Pipeline_ShouldGenerateCorrectMongoDbQuery_WhenUsingFluentPipeline()
    {
        // Arrange - Create test data to validate the query actually works
        var products = new[]
        {
            new TestProduct { Name = "Active Product 1", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Active Product 2", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Inactive Product", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);
        _output.WriteLine($"Inserted {products.Length} test products");

        // Test 1: Verify the pipeline stages are executed in correct order
        var pipelineStages = new object[]
        {
            new Dictionary<string, object> { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new Dictionary<string, object> { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 },
                    { "stock_quantity", 1 },
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(pipelineStages)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        _output.WriteLine($"Pipeline execution returned {results.Count} results");

        // Test 2: Validate the $match stage worked correctly
        results.Should().HaveCount(2, "because only 2 products are active");

        foreach (var result in results)
        {
            _output.WriteLine($"Result: {result["name"]} - Price: {result["price"]} - Inventory Value: {result["inventory_value"]}");
        }

        // Test 3: Validate the $project stage worked correctly
        foreach (var result in results)
        {
            // Should only contain projected fields
            result.Should().ContainKey("name");
            result.Should().ContainKey("price");
            result.Should().ContainKey("stock_quantity");
            result.Should().ContainKey("inventory_value");

            // Should NOT contain non-projected fields
            result.Should().NotContainKey("description");
            result.Should().NotContainKey("category_id");
            result.Should().NotContainKey("tags");
            result.Should().NotContainKey("metadata");
        }

        // Test 4: Validate the calculated field ($multiply) worked correctly
        foreach (var result in results)
        {
            var price = Convert.ToDecimal(result["price"]);
            var stockQuantity = Convert.ToInt32(result["stock_quantity"]);
            var inventoryValue = Convert.ToDecimal(result["inventory_value"]);
            var expectedValue = price * stockQuantity;

            inventoryValue.Should().Be(expectedValue,
                $"because inventory_value should be price ({price}) * stock_quantity ({stockQuantity}) = {expectedValue}");
        }

        // Test 5: Verify specific calculations
        var activeProduct1 = results.FirstOrDefault(r => r["name"].ToString() == "Active Product 1");
        activeProduct1.Should().NotBeNull();
        Convert.ToDecimal(activeProduct1!["inventory_value"]).Should().Be(1000.00m, "because 100 * 10 = 1000");

        var activeProduct2 = results.FirstOrDefault(r => r["name"].ToString() == "Active Product 2");
        activeProduct2.Should().NotBeNull();
        Convert.ToDecimal(activeProduct2!["inventory_value"]).Should().Be(1000.00m, "because 200 * 5 = 1000");
    }

    [Fact]
    public async Task Pipeline_ShouldExecuteStagesInCorrectOrder_WhenCombiningWithAdditionalStages()
    {
        // Arrange - Test that pipeline stages are executed in the correct order
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true, StockQuantity = 2 },
            new TestProduct { Name = "Inactive Product", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Pipeline stages from .Pipeline() method
        var initialPipeline = new object[]
        {
            new Dictionary<string, object> { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new Dictionary<string, object> { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 },
                    { "stock_quantity", 1 },
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } }
                }
            }}
        };

        // Additional pipeline stages passed to AggregateAsync
        var additionalPipeline = new Dictionary<string, object>[]
        {
            new() { { "$sort", new Dictionary<string, object> { { "inventory_value", -1 } } } },
            new() { { "$limit", 2 } }
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(initialPipeline)
            .AggregateAsync<Dictionary<string, object>>(additionalPipeline);

        _output.WriteLine($"Combined pipeline execution returned {results.Count} results");

        // Should return 2 results (due to $limit: 2)
        results.Should().HaveCount(2);

        // Should be sorted by inventory_value descending
        var first = results[0];
        var second = results[1];

        var firstInventoryValue = Convert.ToDecimal(first["inventory_value"]);
        var secondInventoryValue = Convert.ToDecimal(second["inventory_value"]);

        _output.WriteLine($"First result: {first["name"]} - Inventory Value: {firstInventoryValue}");
        _output.WriteLine($"Second result: {second["name"]} - Inventory Value: {secondInventoryValue}");

        firstInventoryValue.Should().BeGreaterOrEqualTo(secondInventoryValue,
            "because results should be sorted by inventory_value descending");

        // Verify the expected order (Product A: 1000, Product B: 1000, Product C: 600)
        // Since Product A and B both have 1000, either could be first, but both should be > Product C
        firstInventoryValue.Should().Be(1000.00m);
        secondInventoryValue.Should().Be(1000.00m);
    }

    [Fact]
    public async Task Pipeline_ShouldWorkCorrectly_WhenUsingEmptyAdditionalPipeline()
    {
        // Arrange - Test the exact scenario from the failing test
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Product C", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // This is the exact scenario from the original test
        var pipelineStages = new object[]
        {
            new Dictionary<string, object> { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new Dictionary<string, object> { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 },
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } }
                }
            }}
        };

        // The key test: empty array passed to AggregateAsync
        var results = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(pipelineStages)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        _output.WriteLine($"Empty additional pipeline test returned {results.Count} results");

        // Verify the pipeline stages from .Pipeline() were executed
        results.Should().HaveCount(2, "because the $match stage should filter to only active products");

        // Verify all results are active products (the $match worked)
        foreach (var result in results)
        {
            var name = result["name"].ToString();
            name.Should().NotBe("Product C", "because Product C is inactive and should be filtered out by $match");
        }

        // Verify the $project and calculated field worked
        foreach (var result in results)
        {
            result.Should().ContainKey("inventory_value");
            var inventoryValue = Convert.ToDecimal(result["inventory_value"]);
            inventoryValue.Should().BeGreaterThan(0, "because inventory_value should be calculated correctly");
        }
    }
}