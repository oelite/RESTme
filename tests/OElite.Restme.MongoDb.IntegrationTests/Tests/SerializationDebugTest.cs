using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Debug serialization issues to understand why decimals become strings in aggregation
/// </summary>
public class SerializationDebugTest : TestBase
{
    private readonly ITestOutputHelper _output;

    public SerializationDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Debug_ShouldTestSerializationTypes_WhenUsingDifferentQueryMethods()
    {
        // Arrange - Create a single test product
        var product = new TestProduct
        {
            Name = "Test Product",
            Price = 123.45m,
            IsActive = true,
            StockQuantity = 10
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertOneAsync(product);
        _output.WriteLine($"Inserted product with Price: {product.Price} (type: {product.Price.GetType()})");

        // Test 1: Query using typed result (TestProduct)
        var typedResult = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == product.Id)
            .FirstOrDefaultAsync();

        if (typedResult != null)
        {
            _output.WriteLine($"Typed query - Price: {typedResult.Price} (type: {typedResult.Price.GetType()})");
        }

        // Test 2: Query using Dictionary<string, object> result
        var dictResults = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == product.Id)
            .AggregateAsync<Dictionary<string, object>>(new Dictionary<string, object>[0]);

        if (dictResults.Any())
        {
            var dictResult = dictResults.First();
            var priceValue = dictResult.GetValueOrDefault("price");
            _output.WriteLine($"Dictionary query - Price: {priceValue} (type: {priceValue?.GetType()})");

            foreach (var kvp in dictResult)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value} (type: {kvp.Value?.GetType()})");
            }
        }

        // Test 3: Simple aggregation match with Dictionary result
        var matchPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "_id", product.Id } } } }
        };

        var matchResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(matchPipeline);

        if (matchResults.Any())
        {
            var matchResult = matchResults.First();
            var priceValue = matchResult.GetValueOrDefault("price");
            _output.WriteLine($"Aggregation match - Price: {priceValue} (type: {priceValue?.GetType()})");

            foreach (var kvp in matchResult)
            {
                if (kvp.Key == "price" || kvp.Key == "stock_quantity" || kvp.Key == "is_active")
                {
                    _output.WriteLine($"  {kvp.Key}: {kvp.Value} (type: {kvp.Value?.GetType()})");
                }
            }
        }

        // Test 4: Aggregation project to see what types come out
        var projectPipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object> { { "_id", product.Id } } } },
            new() { { "$project", new Dictionary<string, object>
                {
                    { "price", 1 },
                    { "stock_quantity", 1 },
                    { "price_type", new Dictionary<string, object> { { "$type", "$price" } } },
                    { "stock_type", new Dictionary<string, object> { { "$type", "$stock_quantity" } } }
                }
            }}
        };

        var projectResults = await DbCentre.GetQuery<TestProduct>().AggregateAsync<Dictionary<string, object>>(projectPipeline);

        if (projectResults.Any())
        {
            var projectResult = projectResults.First();
            _output.WriteLine($"Aggregation project results:");
            foreach (var kvp in projectResult)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value} (type: {kvp.Value?.GetType()})");
            }
        }

        // Assert that basic functionality works
        typedResult.Should().NotBeNull();
        dictResults.Should().HaveCount(1);
        matchResults.Should().HaveCount(1);
        projectResults.Should().HaveCount(1);
    }
}