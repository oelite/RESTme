using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Integration tests for MongoDbQueryOptions (typed sort/pagination) and MongoDbBulkInsertResult.
/// Tests against the shared oelite-stack MongoDB instance.
/// These tests are marked as Integration and will be excluded from CI runs.
/// </summary>
[Trait("Category", "Integration")]
public class MongoDbTypedQueryOptionsIntegrationTests : TestBase
{
    #region FindPagedAsync with Sort and Paging

    [Fact]
    public async Task FindPagedAsync_WithOrderByAscending_SortsResultsCorrectly()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true },
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true }
        };
        await collection.InsertManyAsync(products);

        var options = MongoDbQueryOptions<TestProduct>.OrderBy(p => p.Price);

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert
        result.Documents.Should().HaveCount(3);
        result.Documents[0].Name.Should().Be("Product A");
        result.Documents[1].Name.Should().Be("Product B");
        result.Documents[2].Name.Should().Be("Product C");
        result.TotalCount.Should().Be(3);
        result.Skip.Should().Be(0);
        result.PageSize.Should().Be(0);
    }

    [Fact]
    public async Task FindPagedAsync_WithOrderByDescending_SortsResultsDescending()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product B", Price = 200.00m, IsActive = true },
            new TestProduct { Name = "Product C", Price = 300.00m, IsActive = true }
        };
        await collection.InsertManyAsync(products);

        var options = MongoDbQueryOptions<TestProduct>.OrderByDescending(p => p.Price);

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert
        result.Documents.Should().HaveCount(3);
        result.Documents[0].Name.Should().Be("Product C");
        result.Documents[1].Name.Should().Be("Product B");
        result.Documents[2].Name.Should().Be("Product A");
    }

    [Fact]
    public async Task FindPagedAsync_WithPaging_ReturnsCorrectPage()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();
        await collection.InsertManyAsync(products);

        // First page: skip=0, limit=3
        var pageOneOptions = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 0, limit: 3);

        // Act
        var pageOne = await collection.FindPagedAsync(p => p.IsActive, pageOneOptions);

        // Assert
        pageOne.Documents.Should().HaveCount(3);
        pageOne.Documents[0].Price.Should().Be(10m);
        pageOne.Documents[1].Price.Should().Be(20m);
        pageOne.Documents[2].Price.Should().Be(30m);
        pageOne.TotalCount.Should().Be(10);
        pageOne.Skip.Should().Be(0);
        pageOne.PageSize.Should().Be(3);
        pageOne.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task FindPagedAsync_WithPaging_SkipMidCollection()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();
        await collection.InsertManyAsync(products);

        // Second page: skip=3, limit=3
        var pageTwoOptions = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 3, limit: 3);

        // Act
        var pageTwo = await collection.FindPagedAsync(p => p.IsActive, pageTwoOptions);

        // Assert
        pageTwo.Documents.Should().HaveCount(3);
        pageTwo.Documents[0].Price.Should().Be(40m);
        pageTwo.Documents[1].Price.Should().Be(50m);
        pageTwo.Documents[2].Price.Should().Be(60m);
        pageTwo.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task FindPagedAsync_WithPaging_LastPage()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = Enumerable.Range(1, 10)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();
        await collection.InsertManyAsync(products);

        // Last page: skip=9, limit=3
        var lastPageOptions = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 9, limit: 3);

        // Act
        var lastPage = await collection.FindPagedAsync(p => p.IsActive, lastPageOptions);

        // Assert
        lastPage.Documents.Should().HaveCount(1);
        lastPage.Documents[0].Price.Should().Be(100m);
        lastPage.HasNextPage.Should().BeFalse();
        lastPage.Skip.Should().Be(9);
    }

    [Fact]
    public async Task FindPagedAsync_WithPaging_SkipBeyondResults()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product 1", Price = 10m, IsActive = true },
            new TestProduct { Name = "Product 2", Price = 20m, IsActive = true }
        };
        await collection.InsertManyAsync(products);

        var options = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 100, limit: 10);

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert
        result.Documents.Should().BeEmpty();
        result.TotalCount.Should().Be(2);
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task FindPagedAsync_WithPaging_LimitZero_NoLimit()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = Enumerable.Range(1, 5)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10.00m,
                IsActive = true
            })
            .ToArray();
        await collection.InsertManyAsync(products);

        var options = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 0, limit: 0); // No limit

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert
        result.Documents.Should().HaveCount(5);
        result.PageSize.Should().Be(0);
        result.HasNextPage.Should().BeFalse(); // Limit=0 means no paging
    }

    [Fact]
    public async Task FindPagedAsync_TotalCountFiltersCorrectly()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Active 1", Price = 50m, IsActive = true },
            new TestProduct { Name = "Active 2", Price = 100m, IsActive = true },
            new TestProduct { Name = "Inactive 1", Price = 200m, IsActive = false }
        };
        await collection.InsertManyAsync(products);

        var options = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .Page(skip: 0, limit: 1);

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert
        result.Documents.Should().HaveCount(1);
        result.TotalCount.Should().Be(2); // Only active products
        result.HasNextPage.Should().BeTrue(); // 2 total, returning 1
    }

    [Fact]
    public async Task FindPagedAsync_WithThenBy_ReplacesSort()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product A", Price = 100m, StockQuantity = 50, IsActive = true },
            new TestProduct { Name = "Product B", Price = 100m, StockQuantity = 10, IsActive = true },
            new TestProduct { Name = "Product C", Price = 200m, StockQuantity = 30, IsActive = true }
        };
        await collection.InsertManyAsync(products);

        // OrderBy Price, then replace with ThenBy Stock
        var options = MongoDbQueryOptions<TestProduct>
            .OrderBy(p => p.Price)
            .ThenBy(p => p.StockQuantity); // Replaces Price sort

        // Act
        var result = await collection.FindPagedAsync(p => p.IsActive, options);

        // Assert — sorted by stock (ascending)
        result.Documents.Should().HaveCount(3);
        result.Documents[0].StockQuantity.Should().Be(10);
        result.Documents[1].StockQuantity.Should().Be(30);
        result.Documents[2].StockQuantity.Should().Be(50);
    }

    #endregion

    #region InsertManyAsync Tests

    [Fact]
    public async Task InsertManyAsync_AllInserted_ReturnsSuccess()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product 1", Price = 10m, IsActive = true },
            new TestProduct { Name = "Product 2", Price = 20m, IsActive = true },
            new TestProduct { Name = "Product 3", Price = 30m, IsActive = true }
        };

        // Act
        var result = await collection.InsertManyAsync(products);

        // Assert
        result.InsertedCount.Should().Be(3);
        result.DuplicateCount.Should().Be(0);
        result.AllInserted.Should().BeTrue();
        result.AnyInserted.Should().BeTrue();
        result.AnyDuplicates.Should().BeFalse();
    }

    [Fact]
    public async Task InsertManyAsync_DuplicateKeyAtPosition_FailsAfterInsertedPrefix()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var firstBatch = new[]
        {
            new TestProduct { Id = DbObjectId.NewId(), Name = "Existing 1", Price = 10m, IsActive = true }
        };
        await collection.InsertManyAsync(firstBatch);

        var secondBatch = new[]
        {
            new TestProduct { Name = "New 1", Price = 100m, IsActive = true },
            new TestProduct { Name = "New 2", Price = 200m, IsActive = true },
            new TestProduct { Id = firstBatch[0].Id, Name = "Dup Existing 1", Price = 500m, IsActive = true }, // Duplicate key
            new TestProduct { Name = "New 3", Price = 300m, IsActive = true }
        };

        // Act
        try
        {
            await collection.InsertManyAsync(secondBatch);
            // Should throw exception before reaching here
            Assert.True(false, "Expected MongoDbBulkInsertException");
        }
        catch (MongoDbBulkInsertException ex)
        {
            // Assert
            ex.Failure.Should().Be(MongoDbBulkInsertFailure.DuplicateKey);
            ex.InsertedCount.Should().Be(2); // The two "New" items before the duplicate
            var verifyResults = await collection.FindPagedAsync(p => p.IsActive, (MongoDbQueryOptions<TestProduct>?)null);
            verifyResults.Should().HaveCount(3); // 1 original + 2 newly inserted
        }
    }

    [Fact]
    public async Task InsertManyAsync_EmptyList_ReturnsZeroCounts()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var emptyList = Array.Empty<TestProduct>();

        // Act
        var result = await collection.InsertManyAsync(emptyList);

        // Assert
        result.InsertedCount.Should().Be(0);
        result.DuplicateCount.Should().Be(0);
        result.AllInserted.Should().BeTrue(); // No failures
        result.AnyInserted.Should().BeFalse();
    }

    [Fact]
    public async Task InsertManyAsync_AssignsIdToDocumentsWithoutId()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var products = new[]
        {
            new TestProduct { Name = "Product 1", Price = 10m, IsActive = true },
            new TestProduct { Name = "Product 2", Price = 20m, IsActive = true }
        };

        // Act
        var result = await collection.InsertManyAsync(products);

        // Assert
        result.AllInserted.Should().BeTrue();
        products[0].Id.Should().NotBe(DbObjectId.Empty);
        products[1].Id.Should().NotBe(DbObjectId.Empty);
        products[0].Id.Should().NotBe(products[1].Id);

        // Verify persistence
        var result = await collection.FindPagedAsync(p => true, (MongoDbQueryOptions<TestProduct>?)null);
        result.Documents.Should().HaveCount(2);
        result.Documents.All(p => !p.Id.IsNullOrEmpty()).Should().BeTrue();
    }

    [Fact]
    public async Task InsertManyAsync_PreservesExistingId()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        var id1 = DbObjectId.NewId();
        var id2 = DbObjectId.NewId();
        var products = new[]
        {
            new TestProduct { Id = id1, Name = "Product 1", Price = 10m, IsActive = true },
            new TestProduct { Id = id2, Name = "Product 2", Price = 20m, IsActive = true }
        };

        // Act
        var result = await collection.InsertManyAsync(products);

        // Assert
        result.AllInserted.Should().BeTrue();
        products[0].Id.Should().Be(id1);
        products[1].Id.Should().Be(id2);

        var result = await collection.FindPagedAsync(p => true, (MongoDbQueryOptions<TestProduct>?)null);
        result.Documents.Select(p => p.Id).Should().Contain(new[] { id1, id2 });
    }

    [Fact]
    public async Task InsertManyAsync_LargeOrdered_PartialInsertOnDuplicate()
    {
        // Arrange
        var collection = GetDbCollection<TestProduct>();
        const int batchSize = 100;
        var products = Enumerable.Range(0, batchSize)
            .Select(i => new TestProduct
            {
                Name = $"Product {i}",
                Price = i * 10m,
                IsActive = true
            })
            .ToList();

        // Insert first batch
        var firstResult = await collection.InsertManyAsync(products);
        firstResult.AllInserted.Should().BeTrue();

        // Try inserting the same batch again (all duplicates)
        try
        {
            await collection.InsertManyAsync(products);
            // Should fail
            Assert.True(false, "Expected MongoDbBulkInsertException");
        }
        catch (MongoDbBulkInsertException ex)
        {
            // Assert
            ex.Failure.Should().Be(MongoDbBulkInsertFailure.DuplicateKey);
            ex.InsertedCount.Should().Be(0); // First item is duplicate

            var allStored = await collection.FindPagedAsync(p => true, (MongoDbQueryOptions<TestProduct>?)null);
            allStored.Documents.Should().HaveCount(batchSize); // Still only the original batch
        }
    }

    #endregion
}
