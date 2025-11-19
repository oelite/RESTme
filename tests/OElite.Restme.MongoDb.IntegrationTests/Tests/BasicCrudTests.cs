using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Basic CRUD integration tests for OElite.Restme.MongoDb operations
/// Tests the core functionality using the actual API
/// </summary>
public class BasicCrudTests : TestBase
{
    [Fact]
    public async Task Product_ShouldCreateReadUpdateDelete_WhenUsingBasicOperations()
    {
        // Arrange
        // Clear collection to ensure clean test state
        var cleanupQuery = DbCentre.GetQuery<TestProduct>();
        await cleanupQuery.Query(p => true).DeleteManyAsync();

        var originalProduct = new TestProduct
        {
            Name = "Test Product",
            Price = 99.99m,
            IsActive = true,
            StockQuantity = 10,
            Tags = new List<string> { "test", "electronics" }
        };

        // Act & Assert - Create
        var query = DbCentre.GetQuery<TestProduct>();
        var insertedProduct = await query.InsertOneAsync(originalProduct);

        insertedProduct.Should().NotBeNull();
        insertedProduct.Id.Should().NotBe(DbObjectId.Empty);
        insertedProduct.Name.Should().Be("Test Product");

        // Act & Assert - Read
        var readQuery = DbCentre.GetQuery<TestProduct>();
        var foundProduct = await readQuery.Query(p => p.Id == insertedProduct.Id).FirstOrDefaultAsync();

        foundProduct.Should().NotBeNull();
        foundProduct!.Name.Should().Be("Test Product");
        foundProduct.Price.Should().Be(99.99m);

        // Act & Assert - Update
        foundProduct.Price = 149.99m;
        foundProduct.Name = "Updated Test Product";

        var updateQuery = DbCentre.GetQuery<TestProduct>();
        var updateResult = await updateQuery
            .Query(p => p.Id == foundProduct.Id)
            .UpdateOneAsync(new Dictionary<string, object>
            {
                { "$set", new Dictionary<string, object>
                    {
                        { "name", foundProduct.Name },
                        { "price", foundProduct.Price }
                    }
                }
            });

        updateResult.ModifiedCount.Should().Be(1);

        // Verify update
        var updatedProduct = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == foundProduct.Id)
            .FirstOrDefaultAsync();

        updatedProduct!.Name.Should().Be("Updated Test Product");
        updatedProduct.Price.Should().Be(149.99m);

        // Act & Assert - Delete
        var deleteQuery = DbCentre.GetQuery<TestProduct>();
        var deleteResult = await deleteQuery
            .Query(p => p.Id == foundProduct.Id)
            .DeleteOneAsync();

        deleteResult.DeletedCount.Should().Be(1);

        // Verify deletion
        var deletedProduct = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Id == foundProduct.Id)
            .FirstOrDefaultAsync();

        deletedProduct.Should().BeNull();
    }

    [Fact]
    public async Task Product_ShouldQueryWithStringFilter_WhenUsingJsonQuery()
    {
        // Arrange
        // Clear collection to ensure clean test state
        var cleanupQuery = DbCentre.GetQuery<TestProduct>();
        await cleanupQuery.Query(p => true).DeleteManyAsync();

        var products = new[]
        {
            new TestProduct { Name = "Electronics Item", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Books Item", Price = 25.00m, IsActive = true },
            new TestProduct { Name = "Inactive Item", Price = 50.00m, IsActive = false }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Query with string filter
        var activeProducts = await DbCentre.GetQuery<TestProduct>()
            .Query("{ 'is_active': true, 'price': { '$gte': 50 } }")
            .ToListAsync();

        // Assert
        activeProducts.Should().HaveCount(1);
        activeProducts[0].Name.Should().Be("Electronics Item");
    }

    [Fact]
    public async Task Product_ShouldQueryWithLambdaFilter_WhenUsingLinqExpression()
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

        // Act - Query with lambda expression
        var expensiveActiveProducts = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.IsActive && p.Price > 50)
            .ToListAsync();

        // Assert
        expensiveActiveProducts.Should().HaveCount(1);
        expensiveActiveProducts[0].Name.Should().Be("Product A");
    }

    [Fact]
    public async Task Product_ShouldQueryWithDictionaryFilter_WhenUsingComplexFilters()
    {
        // Arrange
        // Clear collection to ensure clean test state
        var cleanupQuery = DbCentre.GetQuery<TestProduct>();
        await cleanupQuery.Query(p => true).DeleteManyAsync();

        var products = new[]
        {
            new TestProduct
            {
                Name = "Premium Product",
                Price = 200.00m,
                IsActive = true,
                Tags = new List<string> { "premium", "featured" }
            },
            new TestProduct
            {
                Name = "Standard Product",
                Price = 100.00m,
                IsActive = true,
                Tags = new List<string> { "standard" }
            }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Query with dictionary filter
        var filters = new Dictionary<string, object>
        {
            { "is_active", true },
            { "price", new Dictionary<string, object> { { "$gte", 150.00m } } }
        };

        var premiumProducts = await DbCentre.GetQuery<TestProduct>()
            .Query(filters)
            .ToListAsync();

        // Assert
        premiumProducts.Should().HaveCount(1);
        premiumProducts[0].Name.Should().Be("Premium Product");
    }

    [Fact]
    public async Task Product_ShouldHandlePagination_WhenUsingPaginatedQuery()
    {
        // Arrange
        // Clear collection to ensure clean test state
        var cleanupQuery = DbCentre.GetQuery<TestProduct>();
        await cleanupQuery.Query(p => true).DeleteManyAsync();

        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i:D2}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Get page 2 with 3 items per page
        var paginatedProducts = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.IsActive)
            .Paginated(1, 3, "{ 'price': 1 }") // Page index 1 = second page
            .ToListAsync();

        // Assert
        paginatedProducts.Should().HaveCount(3);
        paginatedProducts[0].Name.Should().Be("Product 04"); // 4th item (page 2, items 4-6)
        paginatedProducts[2].Name.Should().Be("Product 06"); // 6th item
        paginatedProducts.Should().BeInAscendingOrder(p => p.Price);
    }

    [Fact]
    public async Task Product_ShouldHandleParameterizedQueries_WhenUsingParams()
    {
        // Arrange
        // Clear collection to ensure clean test state
        var cleanupQuery = DbCentre.GetQuery<TestProduct>();
        await cleanupQuery.Query(p => true).DeleteManyAsync();

        var categoryId = DbObjectId.NewId();
        var products = new[]
        {
            new TestProduct { Name = "Product 1", CategoryId = categoryId, Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product 2", CategoryId = DbObjectId.NewId(), Price = 150.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act - Use parameterized query
        var parameters = new { CategoryId = categoryId, MinPrice = 50.00m };
        var filteredProducts = await DbCentre.GetQuery<TestProduct>()
            .Query("{ 'category_id': @CategoryId, 'price': { '$gte': @MinPrice } }")
            .Params(parameters)
            .ToListAsync();

        // Assert
        filteredProducts.Should().HaveCount(1);
        filteredProducts[0].Name.Should().Be("Product 1");
    }

    [Fact]
    public async Task Product_ShouldCountDocuments_WhenUsingCountAsync()
    {
        // Arrange
        var products = new[]
        {
            new TestProduct { Name = "Active 1", IsActive = true },
            new TestProduct { Name = "Active 2", IsActive = true },
            new TestProduct { Name = "Inactive 1", IsActive = false }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Act
        var totalCount = await DbCentre.GetQuery<TestProduct>().CountAsync();
        var activeCount = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.IsActive)
            .CountAsync();

        // Assert
        totalCount.Should().Be(3);
        activeCount.Should().Be(2);
    }

    [Fact]
    public async Task Product_ShouldCheckExistence_WhenUsingAnyAsync()
    {
        // Arrange
        var product = new TestProduct { Name = "Expensive Product", Price = 1000.00m, IsActive = true };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertOneAsync(product);

        // Act
        var hasExpensiveProducts = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Price > 500)
            .AnyAsync();

        var hasCheapProducts = await DbCentre.GetQuery<TestProduct>()
            .Query(p => p.Price < 10)
            .AnyAsync();

        // Assert
        hasExpensiveProducts.Should().BeTrue();
        hasCheapProducts.Should().BeFalse();
    }
}