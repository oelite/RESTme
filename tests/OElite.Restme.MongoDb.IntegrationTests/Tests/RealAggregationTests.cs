using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Real aggregation pipeline tests using the actual IMongoQuery aggregation API
/// Tests complex MongoDB operations like $lookup, $group, $project, $unwind
/// </summary>
public class RealAggregationTests : TestBase
{
    [Fact]
    public async Task Aggregation_ShouldExecuteBasicPipeline_WhenUsingAggregateAsync()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Laptop", Price = 1000.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Phone", Price = 500.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Tablet", Price = 300.00m, IsActive = false, StockQuantity = 3 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use AggregateAsync with dictionary pipeline
        var pipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", null },
                    { "total_price", new Dictionary<string, object> { { "$sum", "$price" } } },
                    { "avg_price", new Dictionary<string, object> { { "$avg", "$price" } } },
                    { "count", new Dictionary<string, object> { { "$sum", 1 } } }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(pipeline);

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];
        result.Should().ContainKey("total_price");
        result.Should().ContainKey("avg_price");
        result.Should().ContainKey("count");

        // Should sum only active products: 1000 + 500 = 1500
        Convert.ToDecimal(result["total_price"]).Should().Be(1500.00m);
        Convert.ToInt32(result["count"]).Should().Be(2);
    }

    [Fact]
    public async Task Aggregation_ShouldPerformLookup_WhenJoiningCollections()
    {
        // Arrange
        var categories = new[]
        {
            new TestCategory { Name = "Electronics", IsActive = true },
            new TestCategory { Name = "Books", IsActive = true }
        };

        var categoryQuery = DbCentre.GetQuery<TestCategory>();
        await categoryQuery.InsertManyAsync(categories);

        var products = new[]
        {
            new TestProduct { Name = "Laptop", CategoryId = categories[0].Id, Price = 1000.00m, IsActive = true },
            new TestProduct { Name = "Phone", CategoryId = categories[0].Id, Price = 500.00m, IsActive = true },
            new TestProduct { Name = "Novel", CategoryId = categories[1].Id, Price = 25.00m, IsActive = true }
        };

        var productQuery = DbCentre.GetQuery<TestProduct>();
        await productQuery.InsertManyAsync(products);

        // Act - Use AggregateAsync with Dictionary to handle the joined data structure
        var pipeline = new Dictionary<string, object>[]
        {
            // Match active products first
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },

            // Lookup categories
            new() { { "$lookup", new Dictionary<string, object>
                {
                    { "from", "test_categories" },
                    { "localField", "category_id" },
                    { "foreignField", "_id" },
                    { "as", "category_info" }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(pipeline);

        // Assert
        results.Should().HaveCount(3);
        foreach (var result in results)
        {
            result.Should().ContainKey("category_info");
            var categoryInfo = result["category_info"];
            categoryInfo.Should().NotBeNull();

            // Handle both List<object> and object[] types
            if (categoryInfo is List<object> list)
            {
                list.Count.Should().BeGreaterThan(0);
            }
            else if (categoryInfo is object[] array)
            {
                array.Length.Should().BeGreaterThan(0);
            }
            else
            {
                // Fail with helpful message about the actual type
                throw new ArgumentException($"Expected category_info to be List<object> or object[], but was {categoryInfo?.GetType()}");
            }
        }
    }

    [Fact]
    public async Task Aggregation_ShouldGroupByCategory_WhenUsingGroupMethod()
    {
        // Arrange
        var categoryId1 = DbObjectId.NewId();
        var categoryId2 = DbObjectId.NewId();

        var products = new[]
        {
            new TestProduct { Name = "Laptop", CategoryId = categoryId1, Price = 1000.00m, IsActive = true },
            new TestProduct { Name = "Phone", CategoryId = categoryId1, Price = 500.00m, IsActive = true },
            new TestProduct { Name = "Book1", CategoryId = categoryId2, Price = 25.00m, IsActive = true },
            new TestProduct { Name = "Book2", CategoryId = categoryId2, Price = 30.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use Group aggregation method
        var pipeline = new Dictionary<string, object>[]
        {
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", "$category_id" },
                    { "total_price", new Dictionary<string, object> { { "$sum", "$price" } } },
                    { "avg_price", new Dictionary<string, object> { { "$avg", "$price" } } },
                    { "product_count", new Dictionary<string, object> { { "$sum", 1 } } }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .Match(p => p.IsActive)
            .AggregateAsync<Dictionary<string, object>>(pipeline);

        // Assert
        results.Should().HaveCount(2);

        // Find electronics category result (should have higher total)
        var electronicsResult = results.FirstOrDefault(r => Convert.ToDecimal(r["total_price"]) > 1000);
        electronicsResult.Should().NotBeNull();
        Convert.ToDecimal(electronicsResult!["total_price"]).Should().Be(1500.00m); // 1000 + 500
        Convert.ToInt32(electronicsResult["product_count"]).Should().Be(2);

        // Find books category result
        var booksResult = results.FirstOrDefault(r => Convert.ToDecimal(r["total_price"]) < 100);
        booksResult.Should().NotBeNull();
        Convert.ToDecimal(booksResult!["total_price"]).Should().Be(55.00m); // 25 + 30
        Convert.ToInt32(booksResult["product_count"]).Should().Be(2);
    }

    [Fact]
    public async Task Aggregation_ShouldUnwindArrays_WhenUsingUnwindMethod()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct
            {
                Name = "Multi-tag Product",
                Tags = new List<string> { "electronics", "premium", "featured" },
                Price = 100.00m,
                IsActive = true
            },
            new TestProduct
            {
                Name = "Simple Product",
                Tags = new List<string> { "basic" },
                Price = 50.00m,
                IsActive = true
            }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use aggregation pipeline with $unwind
        var pipeline = new Dictionary<string, object>[]
        {
            new() { { "$unwind", "$tags" } },
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", "$tags" },
                    { "product_count", new Dictionary<string, object> { { "$sum", 1 } } },
                    { "total_price", new Dictionary<string, object> { { "$sum", "$price" } } }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .Match(p => p.IsActive)
            .AggregateAsync<Dictionary<string, object>>(pipeline);

        // Assert
        results.Should().HaveCount(4); // electronics, premium, featured, basic

        // Check that each tag has correct data
        var basicTag = results.FirstOrDefault(r => r["_id"].ToString() == "basic");
        basicTag.Should().NotBeNull();
        Convert.ToInt32(basicTag!["product_count"]).Should().Be(1);
        Convert.ToDecimal(basicTag["total_price"]).Should().Be(50.00m);

        var electronicsTag = results.FirstOrDefault(r => r["_id"].ToString() == "electronics");
        electronicsTag.Should().NotBeNull();
        Convert.ToInt32(electronicsTag!["product_count"]).Should().Be(1);
        Convert.ToDecimal(electronicsTag["total_price"]).Should().Be(100.00m);
    }

    [Fact]
    public async Task Aggregation_ShouldExecuteComplexPipeline_WhenCombiningMultipleStages()
    {
        // Arrange - Setup categories and products with complex relationships
        var categories = new[]
        {
            new TestCategory { Name = "Electronics", IsActive = true },
            new TestCategory { Name = "Books", IsActive = true }
        };

        var categoryQuery = DbCentre.GetQuery<TestCategory>();
        await categoryQuery.InsertManyAsync(categories);

        var products = new[]
        {
            new TestProduct
            {
                Name = "Premium Laptop",
                CategoryId = categories[0].Id,
                Price = 1500.00m,
                IsActive = true,
                Tags = new List<string> { "premium", "electronics" },
                StockQuantity = 5
            },
            new TestProduct
            {
                Name = "Budget Phone",
                CategoryId = categories[0].Id,
                Price = 300.00m,
                IsActive = true,
                Tags = new List<string> { "budget", "electronics" },
                StockQuantity = 10
            },
            new TestProduct
            {
                Name = "Programming Book",
                CategoryId = categories[1].Id,
                Price = 45.00m,
                IsActive = true,
                Tags = new List<string> { "education", "programming" },
                StockQuantity = 20
            }
        };

        var productQuery = DbCentre.GetQuery<TestProduct>();
        await productQuery.InsertManyAsync(products);

        // Act - Complex aggregation pipeline with lookup, match, group, and project
        var pipeline = new Dictionary<string, object>[]
        {
            // Join with categories
            new() { { "$lookup", new Dictionary<string, object>
                {
                    { "from", "test_categories" },
                    { "localField", "category_id" },
                    { "foreignField", "_id" },
                    { "as", "category_info" }
                }
            }},

            // Unwind category info
            new() { { "$unwind", "$category_info" } },

            // Match only active products
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },

            // Group by category
            new() { { "$group", new Dictionary<string, object>
                {
                    { "_id", "$category_info.name" },
                    { "total_products", new Dictionary<string, object> { { "$sum", 1 } } },
                    { "avg_price", new Dictionary<string, object> { { "$avg", "$price" } } },
                    { "max_price", new Dictionary<string, object> { { "$max", "$price" } } },
                    { "total_stock", new Dictionary<string, object> { { "$sum", "$stock_quantity" } } }
                }
            }},

            // Sort by average price descending
            new() { { "$sort", new Dictionary<string, object> { { "avg_price", -1 } } } }
        };

        var results = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(pipeline);

        // Assert
        results.Should().HaveCount(2);

        // Electronics should be first (higher average price)
        var firstResult = results[0];
        firstResult["_id"].ToString().Should().Be("Electronics");
        Convert.ToInt32(firstResult["total_products"]).Should().Be(2);
        Convert.ToDecimal(firstResult["avg_price"]).Should().Be(900.00m); // (1500 + 300) / 2
        Convert.ToDecimal(firstResult["max_price"]).Should().Be(1500.00m);
        Convert.ToInt32(firstResult["total_stock"]).Should().Be(15); // 5 + 10

        // Books should be second
        var secondResult = results[1];
        secondResult["_id"].ToString().Should().Be("Books");
        Convert.ToInt32(secondResult["total_products"]).Should().Be(1);
        Convert.ToDecimal(secondResult["avg_price"]).Should().Be(45.00m);
        Convert.ToInt32(secondResult["total_stock"]).Should().Be(20);
    }

    [Fact]
    public async Task Aggregation_ShouldHandlePipelineMethod_WhenUsingFluentPipeline()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true, StockQuantity = 10 },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true, StockQuantity = 5 },
            new TestProduct { Name = "Product C", Price = 50.00m, IsActive = false, StockQuantity = 15 }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use Pipeline method for fluent aggregation
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

        var results = await DbCentre.GetQuery<TestProduct>()
            .Pipeline(pipelineStages)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        // Assert
        results.Should().HaveCount(2); // Only active products

        foreach (var result in results)
        {
            result.Should().ContainKey("name");
            result.Should().ContainKey("price");
            result.Should().ContainKey("inventory_value");

            var price = Convert.ToDecimal(result["price"]);
            var inventoryValue = Convert.ToDecimal(result["inventory_value"]);

            if (result["name"].ToString() == "Product A")
            {
                price.Should().Be(100.00m);
                inventoryValue.Should().Be(1000.00m); // 100 * 10
            }
            else if (result["name"].ToString() == "Product B")
            {
                price.Should().Be(200.00m);
                inventoryValue.Should().Be(1000.00m); // 200 * 5
            }
        }
    }
}