using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Proves that the Pipeline method fix creates equivalent MongoDB queries
/// by comparing results with manually constructed pipelines
/// </summary>
public class PipelineEquivalenceTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public PipelineEquivalenceTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Pipeline_ShouldProduceEquivalentResults_ToManuallyConstructedPipeline()
    {
        // Arrange - Create identical test data for both approaches
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Product C", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Method 1: Using Pipeline() method (the fix I implemented)
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

        var resultsViaPipelineMethod = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(pipelineStages)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        // Method 2: Manually constructed equivalent pipeline (what should happen)
        var manualPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 },
                    { "stock_quantity", 1 },
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } }
                }
            }}
        };

        var resultsViaManualPipeline = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(manualPipeline);

        _output.WriteLine($"Pipeline method results: {resultsViaPipelineMethod.Count}");
        _output.WriteLine($"Manual pipeline results: {resultsViaManualPipeline.Count}");

        // Both methods should produce identical results
        resultsViaPipelineMethod.Should().HaveCount(resultsViaManualPipeline.Count,
            "because both methods should execute the same MongoDB aggregation pipeline");

        resultsViaPipelineMethod.Should().HaveCount(2, "because both should filter to only active products");

        // Compare the actual content of results
        foreach (var pipelineResult in resultsViaPipelineMethod)
        {
            var matchingManualResult = resultsViaManualPipeline.FirstOrDefault(mr =>
                mr["name"].ToString() == pipelineResult["name"].ToString());

            matchingManualResult.Should().NotBeNull($"Manual pipeline should have product {pipelineResult["name"]}");

            // Compare all fields
            pipelineResult["price"].Should().Be(matchingManualResult!["price"]);
            pipelineResult["stock_quantity"].Should().Be(matchingManualResult["stock_quantity"]);
            pipelineResult["inventory_value"].Should().Be(matchingManualResult["inventory_value"]);
        }

        _output.WriteLine("✅ Both methods produce identical results, confirming the fix creates correct MongoDB queries");
    }

    [Fact]
    public async Task Pipeline_ShouldCorrectlyCombine_PipelineMethodAndAdditionalStages()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true, StockQuantity = 2 },
            new TestProduct { Name = "Inactive", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Method 1: Using Pipeline() + additional stages (the combination scenario)
        var initialStages = new object[]
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

        var additionalStages = new Dictionary<string, object>[]
        {
            new() { { "$sort", new Dictionary<string, object> { { "inventory_value", -1 } } } },
            new() { { "$limit", 2 } }
        };

        var combinedResults = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(initialStages)
            .AggregateAsync<Dictionary<string, object>>(additionalStages);

        // Method 2: Equivalent manual pipeline (all stages in one array)
        var equivalentManualPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 },
                    { "stock_quantity", 1 },
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } }
                }
            }},
            new() { { "$sort", new Dictionary<string, object> { { "inventory_value", -1 } } } },
            new() { { "$limit", 2 } }
        };

        var manualResults = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(equivalentManualPipeline);

        _output.WriteLine($"Combined method results: {combinedResults.Count}");
        _output.WriteLine($"Manual equivalent results: {manualResults.Count}");

        // Both should return exactly 2 results (due to $limit: 2)
        combinedResults.Should().HaveCount(2);
        manualResults.Should().HaveCount(2);

        // Both should have the same results in the same order
        for (int i = 0; i < combinedResults.Count; i++)
        {
            var combinedResult = combinedResults[i];
            var manualResult = manualResults[i];

            combinedResult["name"].Should().Be(manualResult["name"],
                $"Result {i} should have the same name in both methods");
            combinedResult["inventory_value"].Should().Be(manualResult["inventory_value"],
                $"Result {i} should have the same inventory_value in both methods");
        }

        _output.WriteLine("✅ Combined pipeline execution produces identical results to manual equivalent");
    }

    [Fact]
    public async Task Pipeline_ShouldWorkCorrectly_WithComplexMongoDbOperations()
    {
        // Arrange - Test with complex MongoDB operations to ensure my fix handles all scenarios
        var products = new[]
        {
            new TestProduct { Name = "High Value", Price = 500.00m, IsActive = true, StockQuantity = 20 },
            new TestProduct { Name = "Medium Value", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Low Value", Price = 25.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Inactive High", Price = 1000.00m, IsActive = false, StockQuantity = 50 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Complex pipeline with multiple MongoDB operations
        var complexPipeline = new object[]
        {
            // Stage 1: Match active products
            new Dictionary<string, object> { { "$match", new Dictionary<string, object> { { "is_active", true } } } },

            // Stage 2: Add calculated fields
            new Dictionary<string, object> { { "$addFields", new Dictionary<string, object>
                {
                    { "inventory_value", new Dictionary<string, object> { { "$multiply", new object[] { "$price", "$stock_quantity" } } } },
                    { "value_category", new Dictionary<string, object>
                        {
                            { "$cond", new Dictionary<string, object>
                                {
                                    { "if", new Dictionary<string, object> { { "$gte", new object[] { "$price", 200 } } } },
                                    { "then", "HIGH" },
                                    { "else", new Dictionary<string, object>
                                        {
                                            { "$cond", new Dictionary<string, object>
                                                {
                                                    { "if", new Dictionary<string, object> { { "$gte", new object[] { "$price", 50 } } } },
                                                    { "then", "MEDIUM" },
                                                    { "else", "LOW" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }},

            // Stage 3: Group by value category
            new Dictionary<string, object> { { "$group", new Dictionary<string, object>
                {
                    { "_id", "$value_category" },
                    { "total_inventory_value", new Dictionary<string, object> { { "$sum", "$inventory_value" } } },
                    { "product_count", new Dictionary<string, object> { { "$sum", 1 } } },
                    { "avg_price", new Dictionary<string, object> { { "$avg", "$price" } } }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(complexPipeline)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        _output.WriteLine($"Complex pipeline returned {results.Count} groups");

        foreach (var result in results)
        {
            var category = result["_id"].ToString();
            var totalValue = Convert.ToDecimal(result["total_inventory_value"]);
            var count = Convert.ToInt32(result["product_count"]);
            var avgPrice = Convert.ToDecimal(result["avg_price"]);

            _output.WriteLine($"Category: {category}, Total Value: {totalValue}, Count: {count}, Avg Price: {avgPrice}");
        }

        // Verify the complex aggregation worked correctly
        results.Should().NotBeEmpty("because the complex pipeline should produce grouped results");

        // Should have HIGH (500*20=10000), MEDIUM (100*10=1000), LOW (25*5=125) categories
        var highCategory = results.FirstOrDefault(r => r["_id"].ToString() == "HIGH");
        var mediumCategory = results.FirstOrDefault(r => r["_id"].ToString() == "MEDIUM");
        var lowCategory = results.FirstOrDefault(r => r["_id"].ToString() == "LOW");

        highCategory.Should().NotBeNull("because there should be a HIGH value category");
        Convert.ToDecimal(highCategory!["total_inventory_value"]).Should().Be(10000.00m);

        mediumCategory.Should().NotBeNull("because there should be a MEDIUM value category");
        Convert.ToDecimal(mediumCategory!["total_inventory_value"]).Should().Be(1000.00m);

        lowCategory.Should().NotBeNull("because there should be a LOW value category");
        Convert.ToDecimal(lowCategory!["total_inventory_value"]).Should().Be(125.00m);

        _output.WriteLine("✅ Complex MongoDB aggregation pipeline executed correctly through Pipeline method");
    }
}