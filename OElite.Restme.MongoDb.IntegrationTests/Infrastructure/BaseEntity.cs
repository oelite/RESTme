using OElite;

namespace OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Base entity class for integration test entities
/// Implements the same pattern as OElite.Common.BaseEntity
/// </summary>
public abstract class TestBaseEntity : OElite.BaseEntity
{
    [DbField("created_on_utc")]
    public DateTime CreatedOnUtc { get; set; }

    [DbField("updated_on_utc")]
    public DateTime UpdatedOnUtc { get; set; }

    protected TestBaseEntity()
    {
        Id = DbObjectId.NewId();
        CreatedOnUtc = DateTime.UtcNow;
        UpdatedOnUtc = DateTime.UtcNow;
    }
}