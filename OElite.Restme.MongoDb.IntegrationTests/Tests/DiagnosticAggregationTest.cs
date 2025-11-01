using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Diagnostic test to understand why aggregation lookups are failing
/// </summary>
public class DiagnosticAggregationTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public DiagnosticAggregationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Diagnostic_ShouldShowLookupDetails_WhenDebuggingAggregation()
    {
        // Arrange - Create categories first
        var category = new TestCategory { Name = "Electronics", IsActive = true };
        var categoryQuery = DbCentre.GetQuery<TestCategory>();
        await categoryQuery.InsertOneAsync(category);

        _output.WriteLine($"Created category with ID: {category.Id}");

        // Create product with explicit category reference
        var product = new TestProduct
        {
            Name = "Laptop",
            CategoryId = category.Id,
            Price = 1000.00m,
            IsActive = true
        };
        var productQuery = DbCentre.GetQuery<TestProduct>();
        await productQuery.InsertOneAsync(product);

        _output.WriteLine($"Created product with ID: {product.Id}, CategoryId: {product.CategoryId}");

        // Verify data was inserted correctly
        var insertedCategory = await DbCentre.GetQuery<TestCategory>()
            .Query(c => c.Id == category.Id)
            .FirstOrDefaultAsync();
        var insertedProduct = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == product.Id)
            .FirstOrDefaultAsync();

        _output.WriteLine($"Retrieved category: {insertedCategory?.Name} (ID: {insertedCategory?.Id})");
        _output.WriteLine($"Retrieved product: {insertedProduct?.Name} (CategoryId: {insertedProduct?.CategoryId})");

        // Test basic aggregation first (without lookup)
        var basicPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } }
        };

        var basicResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(basicPipeline);
        _output.WriteLine($"Basic aggregation returned {basicResults.Count} results");

        if (basicResults.Any())
        {
            var first = basicResults.First();
            _output.WriteLine($"First result keys: {string.Join(", ", first.Keys)}");
            _output.WriteLine($"First result category_id: {first.GetValueOrDefault("category_id")}");
        }

        // Test lookup aggregation
        var lookupPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "is_active", true } } } },
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
            var first = lookupResults.First();
            _output.WriteLine($"Lookup result keys: {string.Join(", ", first.Keys)}");
            _output.WriteLine($"Lookup result category_info type: {first.GetValueOrDefault("category_info")?.GetType()}");
            _output.WriteLine($"Lookup result category_info value: {first.GetValueOrDefault("category_info")}");

            if (first["category_info"] is object[] array)
            {
                _output.WriteLine($"category_info array length: {array.Length}");
                if (array.Length > 0)
                {
                    _output.WriteLine($"First category_info item: {array[0]}");
                }
            }
        }

        // Assert basic functionality works
        basicResults.Should().HaveCount(1);
        lookupResults.Should().HaveCount(1);
    }
}