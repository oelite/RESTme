using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

[Collection("Status class-map registry")]
public class BsonSuppressInheritedStatusTest : TestBase
{
    [DbCollection("suppress_status_test")]
    public class SuppressedStatusEntity : BaseEntity
    {
        public new TestStatus Status { get; set; } = TestStatus.Draft;
        public string Name { get; set; } = string.Empty;
    }

    [DbCollection("no_shadow_status_test")]
    public class NoShadowEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [DbCollection("string_shadow_status_test")]
    public class StringShadowEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";
        public string Name { get; set; } = string.Empty;
    }

    public enum TestStatus
    {
        Draft = 0,
        Active = 1,
        Archived = 2
    }

    // --- Invariant: the shared BaseEntity map is NEVER mutated by shadowing types. ---

    [Fact]
    public void ShadowedType_ShouldNotRemoveInheritedStatus_FromBaseClassMap()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>();

        var baseClassMap = BsonClassMap.LookupClassMap(typeof(BaseEntity));
        baseClassMap.Should().NotBeNull();
        baseClassMap.GetMemberMap("Status").Should().NotBeNull(
            "BaseEntity.Status must remain mapped regardless of registration order (restme#12)");
    }

    [Fact]
    public void NoShadowStatus_ShouldKeepInheritedStatus_InBaseClassMap()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<NoShadowEntity>();

        var baseClassMap = BsonClassMap.LookupClassMap(typeof(BaseEntity));
        baseClassMap.Should().NotBeNull();
        baseClassMap.GetMemberMap("Status").Should().NotBeNull(
            "BaseEntity.Status should remain when no 'new' shadow exists");
    }

    // --- Enum shadow, BOTH registration orders. ---

    [Fact]
    public async Task EnumShadow_RegisteredFirst_ShouldSerializeOnlyShadowStatus_NotBaseStatus()
    {
        await RunEnumShadowAsync();
    }

    [Fact]
    public async Task EnumShadow_RegisteredAfterNonShadow_ShouldSerializeOnlyShadowStatus_NotBaseStatus()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<NoShadowEntity>(); // non-shadow first
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>(); // shadow second
        await RunEnumShadowPersistAsync();
    }

    private async Task RunEnumShadowAsync()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>();
        await RunEnumShadowPersistAsync();
    }

    private async Task RunEnumShadowPersistAsync()
    {
        var entity = new SuppressedStatusEntity { Name = "test" };
        entity.Status = TestStatus.Active;
        entity.Id = DbObjectId.NewId();

        var collection = GetRawMongoCollection<SuppressedStatusEntity>("suppress_status_test");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("suppress_status_test")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeFalse(
            "No orphan EntityStatus field should be serialized");
        // Derived element name is the default convention: derivedtype_status
        doc.Contains("suppressedstatusentity_status").Should().BeTrue(
            "Shadow property BSON element should be serialized under derived-type-prefixed name");
        doc["suppressedstatusentity_status"].AsInt32.Should().Be((int)TestStatus.Active);

        var filter = Builders<SuppressedStatusEntity>.Filter.Eq(x => x.Status, TestStatus.Active);
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be(TestStatus.Active);
    }

    // --- String shadow ([DbField("status")]), BOTH registration orders. ---

    [Fact]
    public async Task StringShadow_RegisteredFirst_ShouldTargetStatusElement()
    {
        await RunStringShadowAsync(firstNonShadow: false);
    }

    [Fact]
    public async Task StringShadow_RegisteredAfterNonShadow_ShouldTargetStatusElement()
    {
        await RunStringShadowAsync(firstNonShadow: true);
    }

    private async Task RunStringShadowAsync(bool firstNonShadow)
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        if (firstNonShadow) MongoClassMapConfigurator.RegisterClassMapping<NoShadowEntity>();
        MongoClassMapConfigurator.RegisterClassMapping<StringShadowEntity>();

        var entity = new StringShadowEntity { Name = "test", Status = "pending" };
        entity.Id = DbObjectId.NewId();

        var collection = GetRawMongoCollection<StringShadowEntity>("string_shadow_status_test");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("string_shadow_status_test")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeTrue("derived string Status must persist under element 'status'");
        doc["status"].AsString.Should().Be("pending", "string value must be preserved unchanged");
        // Inherited EntityStatus must not be written.
        // (The only 'status' element is the derived string one above.)

        var filter = Builders<StringShadowEntity>.Filter.Where(d => d.Status == "pending");
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be("pending");

        // Multi-clause filter (Obelisk-shaped repro, note 63273).
        var multi = Builders<StringShadowEntity>.Filter.Where(d => d.Status == "pending" || d.Status == "failed");
        var rendered = multi.Render(new RenderArgs<StringShadowEntity>(
            BsonSerializer.SerializerRegistry.GetSerializer<StringShadowEntity>(),
            BsonSerializer.SerializerRegistry));
        rendered.ToString().Should().Contain("\"status\"");
    }

    // --- Non-shadow persistence byte-identical (inherited EntityStatus). ---

    [Fact]
    public async Task NonShadow_ShouldPersistInheritedEntityStatus_UnderStatusElement()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<NoShadowEntity>();

        var entity = new NoShadowEntity { Name = "test" };
        entity.Status = EntityStatus.Active;
        entity.Id = DbObjectId.NewId();

        var collection = GetRawMongoCollection<NoShadowEntity>("no_shadow_status_test");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("no_shadow_status_test")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsInt32.Should().Be((int)EntityStatus.Active);
    }
}
