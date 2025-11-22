using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Basic tests for MongoQuery extension methods
/// Tests LINQ-style extension methods on IMongoQuery
/// </summary>
public class BasicQueryExtensionTests : TestBase
{
    [Fact]
    public async Task Product_ShouldUseWhereExtension_WhenFilteringResults()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product B", Price = 25.00m, IsActive = true },
            new TestProduct { Name = "Product C", Price = 75.00m, IsActive = false }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use Where extension method
        var filteredProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .Where(p => p.Price > 50)
            .ToListAsync();

        // Assert
        filteredProducts.Should().HaveCount(1);
        filteredProducts[0].Name.Should().Be("Product A");
    }

    [Fact]
    public async Task Product_ShouldUseOrderByExtension_WhenSortingResults()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true },
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use OrderBy extension method
        var sortedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

        // Assert
        sortedProducts.Should().HaveCount(3);
        sortedProducts[0].Name.Should().Be("Product A"); // Lowest price
        sortedProducts[1].Name.Should().Be("Product B");
        sortedProducts[2].Name.Should().Be("Product C"); // Highest price
        sortedProducts.Should().BeInAscendingOrder(p => p.Price);
    }

    [Fact]
    public async Task Product_ShouldUseOrderByDescendingExtension_WhenSortingDescending()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true },
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use OrderByDescending extension method
        var sortedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Price)
            .ToListAsync();

        // Assert
        sortedProducts.Should().HaveCount(3);
        sortedProducts[0].Name.Should().Be("Product C"); // Highest price
        sortedProducts[1].Name.Should().Be("Product B");
        sortedProducts[2].Name.Should().Be("Product A"); // Lowest price
        sortedProducts.Should().BeInDescendingOrder(p => p.Price);
    }

    [Fact]
    public async Task Product_ShouldUseTakeExtension_WhenLimitingResults()
    {
        // Arrange
        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use Take extension method
        var limitedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Take(3)
            .ToListAsync();

        // Assert
        limitedProducts.Should().HaveCount(3);
        limitedProducts[0].Name.Should().Be("Product 1");
        limitedProducts[1].Name.Should().Be("Product 2");
        limitedProducts[2].Name.Should().Be("Product 3");
    }

    [Fact]
    public async Task Product_ShouldUseSkipAndTakeExtensions_WhenPaginating()
    {
        // Arrange
        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use Skip and Take for pagination
        var paginatedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Skip(3) // Skip first 3
            .Take(4) // Take next 4 (items 4-7)
            .ToListAsync();

        // Assert
        paginatedProducts.Should().HaveCount(4);
        paginatedProducts[0].Name.Should().Be("Product 4");
        paginatedProducts[1].Name.Should().Be("Product 5");
        paginatedProducts[2].Name.Should().Be("Product 6");
        paginatedProducts[3].Name.Should().Be("Product 7");
    }

    [Fact]
    public async Task Product_ShouldUseThenByExtension_WhenUsingSecondarySort()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, StockQuantity = 20, IsActive = true },
            new TestProduct { Name = "Product B", Price = 100.00m, StockQuantity = 10, IsActive = true }, // Same price, different stock
            new TestProduct { Name = "Product C", Price = 200.00m, StockQuantity = 5, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use OrderBy with ThenBy for secondary sorting
        var sortedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ThenBy(p => p.StockQuantity) // Secondary sort by stock
            .ToListAsync();

        // Assert
        sortedProducts.Should().HaveCount(3);
        sortedProducts[0].Name.Should().Be("Product B"); // Price 100, Stock 10 (lower stock first)
        sortedProducts[1].Name.Should().Be("Product A"); // Price 100, Stock 20
        sortedProducts[2].Name.Should().Be("Product C"); // Price 200
    }

    [Fact]
    public async Task Product_ShouldUseThenByDescendingExtension_WhenUsingDescendingSecondarySort()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, StockQuantity = 10, IsActive = true },
            new TestProduct { Name = "Product B", Price = 100.00m, StockQuantity = 20, IsActive = true }, // Same price, different stock
            new TestProduct { Name = "Product C", Price = 200.00m, StockQuantity = 5, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use OrderBy with ThenByDescending for secondary sorting
        var sortedProducts = await DbCentre.GetQuery<TestProduct>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ThenByDescending(p => p.StockQuantity) // Secondary sort by stock descending
            .ToListAsync();

        // Assert
        sortedProducts.Should().HaveCount(3);
        sortedProducts[0].Name.Should().Be("Product B"); // Price 100, Stock 20 (higher stock first)
        sortedProducts[1].Name.Should().Be("Product A"); // Price 100, Stock 10
        sortedProducts[2].Name.Should().Be("Product C"); // Price 200
    }
}