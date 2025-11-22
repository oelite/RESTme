using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

namespace OElite.Restme.MongoDb.IntegrationTests.Models;

/// <summary>
/// Test entity representing a product for CRUD operations testing
/// Demonstrates various DbField attribute usages and data types
/// </summary>
[DbCollection("test_products")]
public class TestProduct : TestBaseEntity
{
    [DbField("name")]
    public string Name { get; set; } = string.Empty;

    [DbField("description")]
    public string? Description { get; set; }

    [DbField("price")]
    public decimal Price { get; set; }

    [DbField("category_id")]
    public DbObjectId? CategoryId { get; set; }

    [DbField("is_active")]
    public bool IsActive { get; set; } = true;

    [DbField("tags")]
    public List<string> Tags { get; set; } = new();

    [DbField("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [DbField("stock_quantity")]
    public int StockQuantity { get; set; }

    [DbField("weight_kg")]
    public double? WeightKg { get; set; }

    [DbField("dimensions")]
    public ProductDimensions? Dimensions { get; set; }

    [DbField("supplier_info")]
    public SupplierInfo? SupplierInfo { get; set; }

    [DenormalizedField("test_categories", "{ '_id': @CategoryId }")]
    public TestCategory? Category { get; set; }
}

/// <summary>
/// Embedded document for product dimensions
/// </summary>
public class ProductDimensions
{
    [DbField("length")]
    public double Length { get; set; }

    [DbField("width")]
    public double Width { get; set; }

    [DbField("height")]
    public double Height { get; set; }
}

/// <summary>
/// Embedded document for supplier information
/// </summary>
public class SupplierInfo
{
    [DbField("supplier_name")]
    public string SupplierName { get; set; } = string.Empty;

    [DbField("contact_email")]
    public string? ContactEmail { get; set; }

    [DbField("phone")]
    public string? Phone { get; set; }

    [DbField("address")]
    public Address? Address { get; set; }
}

/// <summary>
/// Embedded document for address
/// </summary>
public class Address
{
    [DbField("street")]
    public string Street { get; set; } = string.Empty;

    [DbField("city")]
    public string City { get; set; } = string.Empty;

    [DbField("state")]
    public string? State { get; set; }

    [DbField("postal_code")]
    public string? PostalCode { get; set; }

    [DbField("country")]
    public string Country { get; set; } = string.Empty;
}