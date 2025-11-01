using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

namespace OElite.Restme.MongoDb.IntegrationTests.Models;

/// <summary>
/// Test entity representing a category for relationship testing
/// Used with denormalized fields in TestProduct
/// </summary>
[DbCollection("test_categories")]
public class TestCategory : TestBaseEntity
{
    [DbField("name")]
    public string Name { get; set; } = string.Empty;

    [DbField("description")]
    public string? Description { get; set; }

    [DbField("parent_category_id")]
    public DbObjectId? ParentCategoryId { get; set; }

    [DbField("is_active")]
    public bool IsActive { get; set; } = true;

    [DbField("sort_order")]
    public int SortOrder { get; set; }

    [DbField("image_url")]
    public string? ImageUrl { get; set; }

    [DbField("seo_keywords")]
    public List<string> SeoKeywords { get; set; } = new();

    [DbField("custom_properties")]
    public Dictionary<string, object> CustomProperties { get; set; } = new();

    [DenormalizedField("test_categories", "{ '_id': @ParentCategoryId }")]
    public TestCategory? ParentCategory { get; set; }

    [DenormalizedCollection("test_categories", "{ 'parent_category_id': @Id }", limit: 100)]
    public List<TestCategory> SubCategories { get; set; } = new();

    [DenormalizedCollection("test_products", "{ 'category_id': @Id, 'is_active': true }", limit: 50)]
    public List<TestProduct> Products { get; set; } = new();
}