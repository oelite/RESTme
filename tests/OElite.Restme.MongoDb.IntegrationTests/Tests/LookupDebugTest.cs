using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Debug lookup operations to understand why category_info is returning null arrays
/// </summary>
public class LookupDebugTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public LookupDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Debug_ShouldTestLookupStepByStep_WhenUsingLookupAggregation()
    {
        // Arrange - Create test data with explicit logging
        var category = new TestCategory { Name = "Electronics", IsActive = true };
        var categoryQuery = DbCentre.GetQuery<TestCategory>();
        await categoryQuery.InsertOneAsync(category);
        _output.WriteLine($"Created category: {category.Name} with ID: {category.Id}");

        var product = new TestProduct
        {
            Name = "Laptop",
            CategoryId = category.Id,
            Price = 1000.00m,
            IsActive = true
        };
        var productQuery = DbCentre.GetQuery<TestProduct>();
        await productQuery.InsertOneAsync(product);
        _output.WriteLine($"Created product: {product.Name} with CategoryId: {product.CategoryId}");

        // Test 1: Verify data exists independently
        var storedCategory = await DbCentre.GetQuery<TestCategory>()
            .Query(c => c.Id == category.Id)
            .FirstOrDefaultAsync();
        var storedProduct = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == product.Id)
            .FirstOrDefaultAsync();

        _output.WriteLine($"Stored category: {storedCategory?.Name} (ID: {storedCategory?.Id})");
        _output.WriteLine($"Stored product: {storedProduct?.Name} (CategoryId: {storedProduct?.CategoryId})");

        // Test 2: Simple aggregation to verify data structure
        var simpleMatch = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "_id", product.Id } } } }
        };

        var simpleResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(simpleMatch);
        if (simpleResults.Any())
        {
            var simple = simpleResults.First();
            _output.WriteLine($"Simple match - category_id: {simple.GetValueOrDefault("category_id")} (type: {simple.GetValueOrDefault("category_id")?.GetType()})");
        }

        // Test 3: Lookup with explicit field mapping
        var lookupPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "_id", product.Id } } } },
            new() { { "$lookup", new Dictionary<string, object>
                {
                    { "from", "test_categories" },
                    { "localField", "category_id" },
                    { "foreignField", "_id" },
                    { "as", "category_info" }
                }
            }}
        };

        var lookupResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(lookupPipeline);
        _output.WriteLine($"Lookup aggregation returned {lookupResults.Count} results");

        if (lookupResults.Any())
        {
            var result = lookupResults.First();
            _output.WriteLine($"Lookup result keys: {string.Join(", ", result.Keys)}");

            var categoryInfo = result.GetValueOrDefault("category_info");
            _output.WriteLine($"category_info: {categoryInfo} (type: {categoryInfo?.GetType()})");

            if (categoryInfo is List<object> list)
            {
                _output.WriteLine($"category_info is List<object> with {list.Count} items");
                if (list.Count > 0)
                {
                    _output.WriteLine($"First item: {list[0]} (type: {list[0]?.GetType()})");
                }
            }
            else if (categoryInfo is object[] array)
            {
                _output.WriteLine($"category_info is object[] with {array.Length} items");
                if (array.Length > 0)
                {
                    _output.WriteLine($"First item: {array[0]} (type: {array[0]?.GetType()})");
                }
            }
        }

        // Test 4: Manual verification - check if categories exist by direct query
        var allCategories = await DbCentre.GetQuery<TestCategory>()
            .Query(c => c.IsActive)
            .ToListAsync();
        _output.WriteLine($"Total active categories: {allCategories.Count}");

        // Test 5: Test lookup without match filter to see raw behavior
        var rawLookupPipeline = new Dictionary<string, object>[]
        {
            new() { { "$lookup", new Dictionary<string, object>
                {
                    { "from", "test_categories" },
                    { "localField", "category_id" },
                    { "foreignField", "_id" },
                    { "as", "category_info" }
                }
            }},
            new() { { "$limit", 1 } }
        };

        var rawLookupResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(rawLookupPipeline);
        _output.WriteLine($"Raw lookup (no match) returned {rawLookupResults.Count} results");

        if (rawLookupResults.Any())
        {
            var result = rawLookupResults.First();
            var categoryInfo = result.GetValueOrDefault("category_info");
            _output.WriteLine($"Raw lookup category_info: {categoryInfo} (type: {categoryInfo?.GetType()})");
        }

        // Assert basic functionality
        storedCategory.Should().NotBeNull();
        storedProduct.Should().NotBeNull();
        simpleResults.Should().HaveCount(1);
        lookupResults.Should().HaveCount(1);
    }
}