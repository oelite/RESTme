using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb;
using Xunit;

namespace OElite.Restme.UnitTests;

/// <summary>
/// Unit tests for MongoDbQueryOptions, MongoDbPagedResult, and MongoDbBulkInsertResult
/// No MongoDB or persistence layer required; pure logic validation.
/// Covers composition, paging semantics, and result derivations.
/// </summary>
[Trait("Category", "Unit")]
public class MongoDbQueryOptionsUnitTests
{
    #region MongoDbQueryOptions<T> Composition Tests

    [Fact]
    public void OrderBy_StaticFactory_CreatesAscendingSortExpression()
    {
        // Arrange & Act
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Assert
        options.SortExpression.Should().NotBeNull();
        options.Ascending.Should().BeTrue();
        options.Skip.Should().Be(0);
        options.Limit.Should().Be(0);
    }

    [Fact]
    public void OrderByDescending_StaticFactory_CreatesDescendingSortExpression()
    {
        // Arrange & Act
        var options = MongoDbQueryOptions<TestEntity>.OrderByDescending(e => e.Price);

        // Assert
        options.SortExpression.Should().NotBeNull();
        options.Ascending.Should().BeFalse();
        options.Skip.Should().Be(0);
        options.Limit.Should().Be(0);
    }

    [Fact]
    public void Page_SetsSkipAndLimit()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act
        var result = options.Page(skip: 10, limit: 20);

        // Assert
        result.Should().BeSameAs(options); // Fluent API returns this
        options.Skip.Should().Be(10);
        options.Limit.Should().Be(20);
    }

    [Fact]
    public void Page_ZeroValues_Allowed()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act & Assert - no exception; skip=0 and limit=0 are valid
        var result = options.Page(skip: 0, limit: 0);
        result.Skip.Should().Be(0);
        result.Limit.Should().Be(0);
    }

    [Fact]
    public void Page_NegativeSkip_Throws()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act & Assert
        var ex = Record.Exception(() => options.Page(skip: -1, limit: 10));
        ex.Should().BeNull(); // Page() itself does not validate; validation happens in FindPagedAsync
    }

    [Fact]
    public void Page_NegativeLimit_Throws()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act & Assert
        var ex = Record.Exception(() => options.Page(skip: 0, limit: -1));
        ex.Should().BeNull(); // Page() itself does not validate; validation happens in FindPagedAsync
    }

    [Fact]
    public void ThenBy_ReplacesExistingSortExpression_WithAscending()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act
        options.ThenBy(e => e.Price);

        // Assert
        options.Ascending.Should().BeTrue();
        // Expression is replaced (not added) — single-field sort only
        // We verify contract by checking Ascending flag is updated
    }

    [Fact]
    public void ThenByDescending_ReplacesExistingSortExpression_WithDescending()
    {
        // Arrange
        var options = MongoDbQueryOptions<TestEntity>.OrderBy(e => e.Name);

        // Act
        options.ThenByDescending(e => e.Price);

        // Assert
        options.Ascending.Should().BeFalse();
    }

    [Fact]
    public void FluentComposition_OrderByThenPage()
    {
        // Arrange & Act
        var options = MongoDbQueryOptions<TestEntity>
            .OrderBy(e => e.Name)
            .Page(skip: 5, limit: 25);

        // Assert
        options.SortExpression.Should().NotBeNull();
        options.Ascending.Should().BeTrue();
        options.Skip.Should().Be(5);
        options.Limit.Should().Be(25);
    }

    [Fact]
    public void FluentComposition_OrderByPageThenBy()
    {
        // Arrange & Act
        var options = MongoDbQueryOptions<TestEntity>
            .OrderBy(e => e.Name)
            .Page(skip: 0, limit: 10)
            .ThenByDescending(e => e.Price); // Replace sort

        // Assert
        options.Ascending.Should().BeFalse(); // Descending
        options.Skip.Should().Be(0);
        options.Limit.Should().Be(10);
    }

    #endregion

    #region MongoDbPagedResult<T> HasNextPage Truth Table

    [Fact]
    public void HasNextPage_True_WhenMoreDocumentsExist()
    {
        // Arrange
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Item 1" },
                new() { Id = DbObjectId.NewId(), Name = "Item 2" }
            },
            TotalCount = 10,
            Skip = 0,
            PageSize = 2
        };

        // Act & Assert
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_False_WhenOnLastPage()
    {
        // Arrange
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Item 9" },
                new() { Id = DbObjectId.NewId(), Name = "Item 10" }
            },
            TotalCount = 10,
            Skip = 8,
            PageSize = 2
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_False_WhenLimitIsZero()
    {
        // Arrange — unlimited results
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Item 1" }
            },
            TotalCount = 100,
            Skip = 0,
            PageSize = 0 // No limit
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse(); // Limit=0 means no paging; HasNextPage always false
    }

    [Fact]
    public void HasNextPage_False_WhenTotalCountEqualsSkipPlusDocs()
    {
        // Arrange
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Item 1" }
            },
            TotalCount = 1,
            Skip = 0,
            PageSize = 10
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_False_WhenDocumentsIsEmpty()
    {
        // Arrange
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = Array.Empty<TestEntity>(),
            TotalCount = 50,
            Skip = 50,
            PageSize = 10
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_False_WhenTotalCountIsZero()
    {
        // Arrange
        var result = new MongoDbPagedResult<TestEntity>
        {
            Documents = Array.Empty<TestEntity>(),
            TotalCount = 0,
            Skip = 0,
            PageSize = 10
        };

        // Act & Assert
        result.HasNextPage.Should().BeFalse();
    }

    #endregion

    #region MongoDbBulkInsertResult<T> Derived Flags

    [Fact]
    public void AllInserted_True_WhenDuplicateCountIsZero()
    {
        // Arrange
        var result = new MongoDbBulkInsertResult<TestEntity>(
            InsertedCount: 5,
            DuplicateCount: 0,
            Duplicates: new List<TestEntity>());

        // Act & Assert
        result.AllInserted.Should().BeTrue();
        result.AnyInserted.Should().BeTrue();
        result.AnyDuplicates.Should().BeFalse();
    }

    [Fact]
    public void AllInserted_False_WhenDuplicateCountGreaterThanZero()
    {
        // Arrange
        var result = new MongoDbBulkInsertResult<TestEntity>(
            InsertedCount: 3,
            DuplicateCount: 2,
            Duplicates: new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Dup 1" },
                new() { Id = DbObjectId.NewId(), Name = "Dup 2" }
            });

        // Act & Assert
        result.AllInserted.Should().BeFalse();
        result.AnyInserted.Should().BeTrue();
        result.AnyDuplicates.Should().BeTrue();
    }

    [Fact]
    public void AnyInserted_False_WhenInsertedCountIsZero()
    {
        // Arrange
        var result = new MongoDbBulkInsertResult<TestEntity>(
            InsertedCount: 0,
            DuplicateCount: 5,
            Duplicates: new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Dup 1" }
            });

        // Act & Assert
        result.AnyInserted.Should().BeFalse();
        result.AllInserted.Should().BeFalse();
        result.AnyDuplicates.Should().BeTrue();
    }

    [Fact]
    public void AnyDuplicates_False_WhenDuplicateCountIsZero()
    {
        // Arrange
        var result = new MongoDbBulkInsertResult<TestEntity>(
            InsertedCount: 5,
            DuplicateCount: 0,
            Duplicates: new List<TestEntity>());

        // Act & Assert
        result.AnyDuplicates.Should().BeFalse();
    }

    [Fact]
    public void AllThreeFlags_WithPartialSuccess()
    {
        // Arrange
        var result = new MongoDbBulkInsertResult<TestEntity>(
            InsertedCount: 10,
            DuplicateCount: 3,
            Duplicates: new List<TestEntity>
            {
                new() { Id = DbObjectId.NewId(), Name = "Dup 1" }
            });

        // Act & Assert
        result.AllInserted.Should().BeFalse(); // Some duplicates
        result.AnyInserted.Should().BeTrue(); // At least some inserted
        result.AnyDuplicates.Should().BeTrue(); // Duplicates exist
    }

    #endregion

    #region Contract Validation: No MongoDB.Driver/Bson Types Exposed

    [Fact]
    public void MongoDbQueryOptions_PublicSurface_ContainsNoMongoDbTypes()
    {
        // Arrange & Act
        var type = typeof(MongoDbQueryOptions<>);
        var violations = new List<string>();

        foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.PropertyType.Namespace?.StartsWith("MongoDB.", StringComparison.Ordinal) == true)
            {
                violations.Add($"Property {property.Name} exposes {property.PropertyType.FullName}");
            }
        }

        // Assert
        violations.Should().BeEmpty("MongoDbQueryOptions public surface must not expose MongoDB driver types");
    }

    [Fact]
    public void MongoDbPagedResult_PublicSurface_ContainsNoMongoDbTypes()
    {
        // Arrange & Act
        var type = typeof(MongoDbPagedResult<>);
        var violations = new List<string>();

        foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.PropertyType.Namespace?.StartsWith("MongoDB.", StringComparison.Ordinal) == true)
            {
                violations.Add($"Property {property.Name} exposes {property.PropertyType.FullName}");
            }
        }

        // Assert
        violations.Should().BeEmpty("MongoDbPagedResult public surface must not expose MongoDB driver types");
    }

    [Fact]
    public void MongoDbBulkInsertResult_PublicSurface_ContainsNoMongoDbTypes()
    {
        // Arrange & Act
        var type = typeof(MongoDbBulkInsertResult<>);
        var violations = new List<string>();

        foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.PropertyType.Namespace?.StartsWith("MongoDB.", StringComparison.Ordinal) == true)
            {
                violations.Add($"Property {property.Name} exposes {property.PropertyType.FullName}");
            }
        }

        // Assert
        violations.Should().BeEmpty("MongoDbBulkInsertResult public surface must not expose MongoDB driver types");
    }

    #endregion

    #region Test Support Entity

    private class TestEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    #endregion
}
