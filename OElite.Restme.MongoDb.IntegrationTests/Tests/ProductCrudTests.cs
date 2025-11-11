using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Comprehensive CRUD integration tests for TestProduct entity
/// Tests basic MongoDB operations through OElite.Restme.MongoDb
/// </summary>
public class ProductCrudTests : TestBase
{
    [Fact]
    public async Task InsertOneAsync_ShouldInsertProduct_WhenValidProduct()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateTestProduct();

        // Act
        await collection.InsertOneAsync(product);

        // Assert
        var retrievedProduct = await collection.FindOneAsync(p => p.Id == product.Id);
        retrievedProduct.Should().NotBeNull();
        retrievedProduct!.Name.Should().Be(product.Name);
        retrievedProduct.Price.Should().Be(product.Price);
        retrievedProduct.IsActive.Should().Be(product.IsActive);
        retrievedProduct.Tags.Should().BeEquivalentTo(product.Tags);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnProducts_WhenFilterMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            CreateTestProduct("Product 1", price: 10.99m, isActive: true),
            CreateTestProduct("Product 2", price: 20.99m, isActive: true),
            CreateTestProduct("Product 3", price: 30.99m, isActive: false)
        };

        foreach (var product in products)
        {
            await collection.InsertOneAsync(product);
        }

        // Act
        var activeProducts = await collection.FindAsync(p => p.IsActive);

        // Assert
        activeProducts.Should().HaveCount(2);
        activeProducts.All(p => p.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task FindOneAsync_ShouldReturnProduct_WhenIdMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateTestProduct();
        await collection.InsertOneAsync(product);

        // Act
        var retrievedProduct = await collection.FindOneAsync(p => p.Id == product.Id);

        // Assert
        retrievedProduct.Should().NotBeNull();
        retrievedProduct!.Id.Should().Be(product.Id);
        retrievedProduct.Name.Should().Be(product.Name);
    }

    [Fact]
    public async Task FindAsync_WithDictionaryFilter_ShouldReturnMatchingProducts()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateTestProduct("Test Product", price: 25.50m);
        await collection.InsertOneAsync(product);

        // Act
        var filter = new Dictionary<string, object>
        {
            { "name", "Test Product" },
            { "price", 25.50m }
        };
        var results = await collection.FindAsync(filter);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Test Product");
        results[0].Price.Should().Be(25.50m);
    }

    [Fact]
    public async Task ReplaceOneAsync_ShouldUpdateProduct_WhenProductExists()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateTestProduct("Original Product", price: 15.99m);
        await collection.InsertOneAsync(product);

        // Modify the product
        product.Name = "Updated Product";
        product.Price = 19.99m;
        product.UpdatedOnUtc = DateTime.UtcNow;

        // Act
        var result = await collection.ReplaceOneAsync(p => p.Id == product.Id, product);

        // Assert
        result.Should().BeTrue();

        var updatedProduct = await collection.FindOneAsync(p => p.Id == product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Name.Should().Be("Updated Product");
        updatedProduct.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task DeleteManyAsync_ShouldDeleteProducts_WhenFilterMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            CreateTestProduct("Product 1", isActive: false),
            CreateTestProduct("Product 2", isActive: false),
            CreateTestProduct("Product 3", isActive: true)
        };

        foreach (var product in products)
        {
            await collection.InsertOneAsync(product);
        }

        // Act
        var deletedCount = await collection.DeleteManyAsync(p => !p.IsActive);

        // Assert
        deletedCount.Should().Be(2);

        var remainingProducts = await collection.FindAsync(p => true);
        remainingProducts.Should().HaveCount(1);
        remainingProducts[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CountDocumentsAsync_ShouldReturnCorrectCount_WhenFilterApplied()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            CreateTestProduct("Product 1", isActive: true),
            CreateTestProduct("Product 2", isActive: true),
            CreateTestProduct("Product 3", isActive: false),
            CreateTestProduct("Product 4", isActive: false)
        };

        foreach (var product in products)
        {
            await collection.InsertOneAsync(product);
        }

        // Act
        var activeCount = await collection.CountDocumentsAsync(p => p.IsActive);
        var inactiveCount = await collection.CountDocumentsAsync(p => !p.IsActive);
        var totalCount = await collection.CountDocumentsAsync(p => true);

        // Assert
        activeCount.Should().Be(2);
        inactiveCount.Should().Be(2);
        totalCount.Should().Be(4);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenDocumentExists()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateTestProduct("Unique Product");
        await collection.InsertOneAsync(product);

        // Act
        var exists = await collection.ExistsAsync(p => p.Name == "Unique Product");
        var notExists = await collection.ExistsAsync(p => p.Name == "Non-existent Product");

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task InsertOneAsync_ShouldHandleComplexNestedObjects_WhenProductHasEmbeddedDocuments()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var product = CreateComplexTestProduct();

        // Act
        await collection.InsertOneAsync(product);

        // Assert
        var retrievedProduct = await collection.FindOneAsync(p => p.Id == product.Id);
        retrievedProduct.Should().NotBeNull();
        retrievedProduct!.Dimensions.Should().NotBeNull();
        retrievedProduct.Dimensions!.Length.Should().Be(10.5);
        retrievedProduct.SupplierInfo.Should().NotBeNull();
        retrievedProduct.SupplierInfo!.SupplierName.Should().Be("Test Supplier");
        retrievedProduct.SupplierInfo.Address.Should().NotBeNull();
        retrievedProduct.SupplierInfo.Address!.City.Should().Be("New York");
        retrievedProduct.Metadata.Should().ContainKey("category");
        retrievedProduct.Metadata["category"].Should().Be("electronics");
    }

    [Fact]
    public async Task FindAsync_ShouldHandleComplexQueries_WithNestedFieldFiltering()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            CreateTestProduct("Product 1", tags: new[] { "electronics", "mobile" }),
            CreateTestProduct("Product 2", tags: new[] { "clothing", "summer" }),
            CreateTestProduct("Product 3", tags: new[] { "electronics", "computer" })
        };

        foreach (var product in products)
        {
            await collection.InsertOneAsync(product);
        }

        // Act - Find products with "electronics" tag
        var filter = new Dictionary<string, object>
        {
            { "tags", new Dictionary<string, object> { { "$in", new[] { "electronics" } } } }
        };
        var electronicsProducts = await collection.FindAsync(filter);

        // Assert
        electronicsProducts.Should().HaveCount(2);
        electronicsProducts.All(p => p.Tags.Contains("electronics")).Should().BeTrue();
    }

    private static TestProduct CreateTestProduct(
        string name = "Test Product",
        decimal price = 19.99m,
        bool isActive = true,
        string[]? tags = null)
    {
        return new TestProduct
        {
            Name = name,
            Description = $"Description for {name}",
            Price = price,
            IsActive = isActive,
            Tags = tags?.ToList() ?? new List<string> { "test", "sample" },
            StockQuantity = 100,
            WeightKg = 1.5,
            Metadata = new Dictionary<string, object>
            {
                { "brand", "Test Brand" },
                { "model", "v1.0" }
            }
        };
    }

    private static TestProduct CreateComplexTestProduct()
    {
        return new TestProduct
        {
            Name = "Complex Product",
            Description = "A product with complex nested data",
            Price = 199.99m,
            IsActive = true,
            Tags = new List<string> { "electronics", "premium", "featured" },
            StockQuantity = 50,
            WeightKg = 2.3,
            Dimensions = new ProductDimensions
            {
                Length = 10.5,
                Width = 8.2,
                Height = 3.1
            },
            SupplierInfo = new SupplierInfo
            {
                SupplierName = "Test Supplier",
                ContactEmail = "supplier@test.com",
                Phone = "+1-555-0123",
                Address = new Address
                {
                    Street = "123 Test Street",
                    City = "New York",
                    State = "NY",
                    PostalCode = "10001",
                    Country = "USA"
                }
            },
            Metadata = new Dictionary<string, object>
            {
                { "category", "electronics" },
                { "warranty_months", 24 },
                { "features", new[] { "waterproof", "wireless", "fast-charging" } },
                { "ratings", new Dictionary<string, object>
                    {
                        { "average", 4.5 },
                        { "count", 127 }
                    }
                }
            }
        };
    }
}