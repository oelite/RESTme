using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// CRUD integration tests for TestCategory entity
/// Tests hierarchical relationships and category-specific operations
/// </summary>
public class CategoryCrudTests : TestBase
{
    [Fact]
    public async Task InsertOneAsync_ShouldInsertCategory_WhenValidCategory()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var category = CreateTestCategory();

        // Act
        await collection.InsertOneAsync(category);

        // Assert
        var retrievedCategory = await collection.FindOneAsync(c => c.Id == category.Id);
        retrievedCategory.Should().NotBeNull();
        retrievedCategory!.Name.Should().Be(category.Name);
        retrievedCategory.IsActive.Should().Be(category.IsActive);
        retrievedCategory.SortOrder.Should().Be(category.SortOrder);
        retrievedCategory.SeoKeywords.Should().BeEquivalentTo(category.SeoKeywords);
        retrievedCategory.CustomProperties.Should().BeEquivalentTo(category.CustomProperties);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnRootCategories_WhenParentCategoryIdIsNull()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var categories = new[]
        {
            CreateTestCategory("Root Category 1", parentCategoryId: null),
            CreateTestCategory("Root Category 2", parentCategoryId: null),
            CreateTestCategory("Subcategory", parentCategoryId: DbObjectId.NewId())
        };

        foreach (var category in categories)
        {
            await collection.InsertOneAsync(category);
        }

        // Act
        var rootCategories = await collection.FindAsync(c => c.ParentCategoryId == null);

        // Assert
        rootCategories.Should().HaveCount(2);
        rootCategories.All(c => c.ParentCategoryId == null).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_ShouldReturnSubcategories_WhenParentCategoryIdMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var parentCategory = CreateTestCategory("Parent Category");
        await collection.InsertOneAsync(parentCategory);

        var subcategories = new[]
        {
            CreateTestCategory("Subcategory 1", parentCategoryId: parentCategory.Id),
            CreateTestCategory("Subcategory 2", parentCategoryId: parentCategory.Id),
            CreateTestCategory("Other Category", parentCategoryId: DbObjectId.NewId())
        };

        foreach (var subcategory in subcategories)
        {
            await collection.InsertOneAsync(subcategory);
        }

        // Act
        var childCategories = await collection.FindAsync(c => c.ParentCategoryId == parentCategory.Id);

        // Assert
        childCategories.Should().HaveCount(2);
        childCategories.All(c => c.ParentCategoryId == parentCategory.Id).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_WithDictionaryFilter_ShouldFilterBySortOrder()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var categories = new[]
        {
            CreateTestCategory("Category 1", sortOrder: 1),
            CreateTestCategory("Category 2", sortOrder: 2),
            CreateTestCategory("Category 3", sortOrder: 3)
        };

        foreach (var category in categories)
        {
            await collection.InsertOneAsync(category);
        }

        // Act
        var filter = new Dictionary<string, object>
        {
            { "sort_order", new Dictionary<string, object> { { "$lte", 2 } } }
        };
        var results = await collection.FindAsync(filter);

        // Assert
        results.Should().HaveCount(2);
        results.All(c => c.SortOrder <= 2).Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceOneAsync_ShouldUpdateCategory_WhenCategoryExists()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var category = CreateTestCategory("Original Category");
        await collection.InsertOneAsync(category);

        // Modify the category
        category.Name = "Updated Category";
        category.Description = "Updated description";
        category.SortOrder = 99;
        category.UpdatedOnUtc = DateTime.UtcNow;

        // Act
        var result = await collection.ReplaceOneAsync(c => c.Id == category.Id, category);

        // Assert
        result.Should().BeTrue();

        var updatedCategory = await collection.FindOneAsync(c => c.Id == category.Id);
        updatedCategory.Should().NotBeNull();
        updatedCategory!.Name.Should().Be("Updated Category");
        updatedCategory.Description.Should().Be("Updated description");
        updatedCategory.SortOrder.Should().Be(99);
    }

    [Fact]
    public async Task DeleteManyAsync_ShouldDeleteInactiveCategories_WhenFilterMatches()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var categories = new[]
        {
            CreateTestCategory("Active Category 1", isActive: true),
            CreateTestCategory("Inactive Category 1", isActive: false),
            CreateTestCategory("Inactive Category 2", isActive: false),
            CreateTestCategory("Active Category 2", isActive: true)
        };

        foreach (var category in categories)
        {
            await collection.InsertOneAsync(category);
        }

        // Act
        var deletedCount = await collection.DeleteManyAsync(c => !c.IsActive);

        // Assert
        deletedCount.Should().Be(2);

        var remainingCategories = await collection.FindAsync(c => true);
        remainingCategories.Should().HaveCount(2);
        remainingCategories.All(c => c.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task CountDocumentsAsync_ShouldReturnCorrectCounts_ForDifferentFilters()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        // Clear collection to ensure clean test state
        await collection.DeleteManyAsync(c => true);
        var parentCategory = CreateTestCategory("Parent Category");
        await collection.InsertOneAsync(parentCategory);

        var categories = new[]
        {
            CreateTestCategory("Root 1", parentCategoryId: null, isActive: true),
            CreateTestCategory("Root 2", parentCategoryId: null, isActive: false),
            CreateTestCategory("Child 1", parentCategoryId: parentCategory.Id, isActive: true),
            CreateTestCategory("Child 2", parentCategoryId: parentCategory.Id, isActive: true)
        };

        foreach (var category in categories)
        {
            await collection.InsertOneAsync(category);
        }

        // Act
        var totalCount = await collection.CountDocumentsAsync(c => true);
        var rootCount = await collection.CountDocumentsAsync(c => c.ParentCategoryId == null);
        var childCount = await collection.CountDocumentsAsync(c => c.ParentCategoryId == parentCategory.Id);
        var activeCount = await collection.CountDocumentsAsync(c => c.IsActive);

        // Assert
        totalCount.Should().Be(5); // Including the parent category
        rootCount.Should().Be(2);
        childCount.Should().Be(2);
        activeCount.Should().Be(4); // Parent + Root 1 + Child 1 + Child 2
    }

    [Fact]
    public async Task ExistsAsync_ShouldCheckCategoryExistence_ByNameAndStatus()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var category = CreateTestCategory("Unique Category", isActive: true);
        await collection.InsertOneAsync(category);

        // Act
        var existsByName = await collection.ExistsAsync(c => c.Name == "Unique Category");
        var existsByNameAndActive = await collection.ExistsAsync(c => c.Name == "Unique Category" && c.IsActive);
        var existsByNameAndInactive = await collection.ExistsAsync(c => c.Name == "Unique Category" && !c.IsActive);
        var notExists = await collection.ExistsAsync(c => c.Name == "Non-existent Category");

        // Assert
        existsByName.Should().BeTrue();
        existsByNameAndActive.Should().BeTrue();
        existsByNameAndInactive.Should().BeFalse();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task FindAsync_ShouldHandleComplexSeoKeywordSearch_UsingArrayOperators()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var categories = new[]
        {
            CreateTestCategory("Electronics", seoKeywords: new[] { "tech", "gadgets", "electronics" }),
            CreateTestCategory("Clothing", seoKeywords: new[] { "fashion", "apparel", "clothing" }),
            CreateTestCategory("Books", seoKeywords: new[] { "reading", "literature", "books", "tech" })
        };

        foreach (var category in categories)
        {
            await collection.InsertOneAsync(category);
        }

        // Act - Find categories with "tech" keyword
        var filter = new Dictionary<string, object>
        {
            { "seo_keywords", new Dictionary<string, object> { { "$in", new[] { "tech" } } } }
        };
        var techCategories = await collection.FindAsync(filter);

        // Assert
        techCategories.Should().HaveCount(2);
        techCategories.All(c => c.SeoKeywords.Contains("tech")).Should().BeTrue();
    }

    [Fact]
    public async Task InsertOneAsync_ShouldHandleCustomProperties_WithComplexNestedData()
    {
        // Arrange
        var collection = GetDbCollection<TestCategory>();
        var category = CreateComplexTestCategory();

        // Act
        await collection.InsertOneAsync(category);

        // Assert
        var retrievedCategory = await collection.FindOneAsync(c => c.Id == category.Id);
        retrievedCategory.Should().NotBeNull();
        retrievedCategory!.CustomProperties.Should().ContainKey("display_settings");
        retrievedCategory.CustomProperties.Should().ContainKey("filters");
        retrievedCategory.CustomProperties.Should().ContainKey("promotion_rules");

        // Verify nested data integrity
        var displaySettings = retrievedCategory.CustomProperties["display_settings"] as Dictionary<string, object>;
        displaySettings.Should().NotBeNull();
        displaySettings!["grid_columns"].Should().Be(4);
    }

    private static TestCategory CreateTestCategory(
        string name = "Test Category",
        DbObjectId? parentCategoryId = null,
        bool isActive = true,
        int sortOrder = 1,
        string[]? seoKeywords = null)
    {
        return new TestCategory
        {
            Name = name,
            Description = $"Description for {name}",
            ParentCategoryId = parentCategoryId,
            IsActive = isActive,
            SortOrder = sortOrder,
            ImageUrl = $"https://example.com/images/{name.ToLowerInvariant().Replace(" ", "-")}.jpg",
            SeoKeywords = seoKeywords?.ToList() ?? new List<string> { "test", "category", "sample" },
            CustomProperties = new Dictionary<string, object>
            {
                { "display_color", "#3498db" },
                { "icon", "category-icon" },
                { "featured", false }
            }
        };
    }

    private static TestCategory CreateComplexTestCategory()
    {
        return new TestCategory
        {
            Name = "Premium Electronics",
            Description = "High-end electronic devices and accessories",
            ParentCategoryId = null,
            IsActive = true,
            SortOrder = 10,
            ImageUrl = "https://example.com/images/premium-electronics.jpg",
            SeoKeywords = new List<string> { "electronics", "premium", "gadgets", "technology", "high-end" },
            CustomProperties = new Dictionary<string, object>
            {
                { "display_settings", new Dictionary<string, object>
                    {
                        { "grid_columns", 4 },
                        { "show_price_range", true },
                        { "default_sort", "price_desc" },
                        { "thumbnail_size", "large" }
                    }
                },
                { "filters", new[]
                    {
                        new Dictionary<string, object> { { "name", "brand" }, { "type", "multi_select" } },
                        new Dictionary<string, object> { { "name", "price_range" }, { "type", "range" } },
                        new Dictionary<string, object> { { "name", "rating" }, { "type", "rating" } }
                    }
                },
                { "promotion_rules", new Dictionary<string, object>
                    {
                        { "eligible_for_bulk_discount", true },
                        { "min_quantity_for_discount", 3 },
                        { "discount_percentage", 15.0 },
                        { "free_shipping_threshold", 500.00 }
                    }
                },
                { "analytics", new Dictionary<string, object>
                    {
                        { "track_views", true },
                        { "track_clicks", true },
                        { "track_conversions", true },
                        { "google_analytics_category", "Electronics" }
                    }
                }
            }
        };
    }
}