using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

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
        // Intentionally does NOT shadow Status — uses the inherited BaseEntity.Status.
        public string Name { get; set; } = string.Empty;
    }

    public enum TestStatus
    {
        Draft = 0,
        Active = 1,
        Archived = 2
    }

    [Fact]
    public void SuppressAttribute_ShouldRemoveInheritedStatus_FromBaseClassMap()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>();

        var baseClassMap = BsonClassMap.LookupClassMap(typeof(BaseEntity));
        baseClassMap.Should().NotBeNull();
        baseClassMap.GetMemberMap("Status").Should().BeNull(
            "BaseEntity.Status should be auto-unmapped when a 'new' shadow exists");

        var derivedClassMap = BsonClassMap.LookupClassMap(typeof(SuppressedStatusEntity));
        derivedClassMap.Should().NotBeNull();
        derivedClassMap.GetMemberMap("Status").Should().NotBeNull(
            "Derived entity shadow Status should still be mapped");
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

    [Fact]
    public async Task SuppressAttribute_ShouldSerializeOnlyShadowStatus_NotBaseStatus()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>();

        var entity = new SuppressedStatusEntity { Name = "test" };
        entity.Status = TestStatus.Active;
        entity.Id = DbObjectId.NewId();

        var collection = GetRawMongoCollection<SuppressedStatusEntity>("suppress_status_test");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("suppress_status_test").Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString()))).FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeFalse(
            "No orphan EntityStatus field should be serialized");
        doc.Contains("suppressedstatusentity_status").Should().BeTrue(
            "Shadow property BSON element should be serialized under derived-type-prefixed name");

        var statusValue = doc["suppressedstatusentity_status"].AsInt32;
        statusValue.Should().Be((int)TestStatus.Active);
    }

    [Fact]
    public async Task SuppressAttribute_ShouldQueryByShadowStatus_WithoutArgumentException()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<SuppressedStatusEntity>();

        var entity = new SuppressedStatusEntity { Name = "test", Status = TestStatus.Active };
        var collection = GetRawMongoCollection<SuppressedStatusEntity>("suppress_status_test");
        await collection.InsertOneAsync(entity);

        var filter = Builders<SuppressedStatusEntity>.Filter.Eq(x => x.Status, TestStatus.Active);
        var found = await collection.Find(filter).FirstOrDefaultAsync();

        found.Should().NotBeNull();
        found!.Status.Should().Be(TestStatus.Active);
    }
}
