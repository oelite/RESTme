using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Dedicated five-case regression test suite for Restme #12 (oelite/uranus/restme#12),
/// covering the full matrix adjudicated by Marcus in note 63578:
/// 1. String-shadow ([DbField("status")]) — both registration orders (shadow first vs after non-shadow).
/// 2. Enum-shadow ([DbField("dpa_status")]) — both registration orders.
/// 3. Non-shadow entity — persist inherited EntityStatus unchanged (helios/core platform guard).
/// 4. Obelisk-shaped repro — multi-clause filter Where(d => d.Status == "pending" || d.Status == "failed").
/// 5. Roundtrip BSON assertion — element name 'status', string value, zero migration.
/// </summary>
[Collection("Status class-map registry")]
public class ShadowedStatusSerializerTest : TestBase
{
    [DbCollection("order_matrix_string_shadow")]
    public class WebhookDeliveryStyleEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";
        public string Url { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
    }

    [DbCollection("order_matrix_enum_shadow")]
    public class DpaComplianceStyleEntity : BaseEntity
    {
        [DbField("dpa_status")]
        public new DpaStatus Status { get; set; } = DpaStatus.Draft;
        public string AgreementName { get; set; } = string.Empty;
    }

    [DbCollection("order_matrix_non_shadow")]
    public class HeliosCoreProductStyleEntity : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
    }

    public enum DpaStatus
    {
        Draft = 0,
        PendingSignature = 1,
        Active = 2,
        Expired = 3
    }

    // --- CASE 1A: String-shadow, registered FIRST ---
    [Fact]
    public async Task Case1A_StringShadow_RegisteredFirst_LINQ3RendersAndBsonPreserved()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<WebhookDeliveryStyleEntity>();

        await AssertStringShadowBehaviorAsync();
    }

    // --- CASE 1B: String-shadow, registered AFTER unrelated non-shadow entity ---
    [Fact]
    public async Task Case1B_StringShadow_RegisteredAfterNonShadow_LINQ3RendersAndBsonPreserved()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        // Unrelated entity freezes BaseEntity with EntityStatus FIRST.
        MongoClassMapConfigurator.RegisterClassMapping<HeliosCoreProductStyleEntity>();
        // Shadowed entity registered SECOND — this was the exact UAT failure point (restme#12).
        MongoClassMapConfigurator.RegisterClassMapping<WebhookDeliveryStyleEntity>();

        await AssertStringShadowBehaviorAsync();
    }

    // --- CASE 2A: Enum-shadow (distinct element name), registered FIRST ---
    [Fact]
    public async Task Case2A_EnumShadow_RegisteredFirst_LINQ3RendersAndBsonPreserved()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<DpaComplianceStyleEntity>();

        await AssertEnumShadowBehaviorAsync();
    }

    // --- CASE 2B: Enum-shadow, registered AFTER non-shadow entity ---
    [Fact]
    public async Task Case2B_EnumShadow_RegisteredAfterNonShadow_LINQ3RendersAndBsonPreserved()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<HeliosCoreProductStyleEntity>();
        MongoClassMapConfigurator.RegisterClassMapping<DpaComplianceStyleEntity>();

        await AssertEnumShadowBehaviorAsync();
    }

    // --- CASE 3: Non-shadow entity byte-identical persistence ---
    [Fact]
    public async Task Case3_NonShadow_ShouldPersistInheritedEntityStatus_WithoutInterference()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        // Shadow entities registered BEFORE non-shadow to prove no side effects on base map.
        MongoClassMapConfigurator.RegisterClassMapping<WebhookDeliveryStyleEntity>();
        MongoClassMapConfigurator.RegisterClassMapping<HeliosCoreProductStyleEntity>();

        var entity = new HeliosCoreProductStyleEntity
        {
            Id = DbObjectId.NewId(),
            Title = "Helios Core Subscription",
            Status = EntityStatus.Active
        };

        var collection = GetRawMongoCollection<HeliosCoreProductStyleEntity>("order_matrix_non_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("order_matrix_non_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsInt32.Should().Be((int)EntityStatus.Active);

        var filter = Builders<HeliosCoreProductStyleEntity>.Filter.Where(p => p.Status == EntityStatus.Active);
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be(EntityStatus.Active);
    }

    // --- CASE 4: Obelisk-shaped multi-clause Where reproduction (note 63273) ---
    [Fact]
    public void Case4_ObeliskShapedMultiClauseWhere_TranslatesWithoutArgumentException()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<HeliosCoreProductStyleEntity>(); // freeze base
        MongoClassMapConfigurator.RegisterClassMapping<WebhookDeliveryStyleEntity>();

        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<WebhookDeliveryStyleEntity>();
        var filter = Builders<WebhookDeliveryStyleEntity>.Filter.Where(
            d => d.Status == "pending" || d.Status == "failed");

        var rendered = filter.Render(new RenderArgs<WebhookDeliveryStyleEntity>(
            serializer,
            BsonSerializer.SerializerRegistry));

        var renderedJson = rendered.ToString();
        renderedJson.Should().Contain("\"status\"");
        renderedJson.Should().Contain("\"pending\"");
        renderedJson.Should().Contain("\"failed\"");
    }

    // --- CASE 5: Round-trip preserves raw BSON values and element names ---
    [Fact]
    public async Task Case5_RawBsonRoundTrip_PreservesStoredFieldsWithZeroDataMigration()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<HeliosCoreProductStyleEntity>();
        MongoClassMapConfigurator.RegisterClassMapping<WebhookDeliveryStyleEntity>();

        var entity = new WebhookDeliveryStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = "permanent_failure",
            Url = "https://partner.example.com/webhook",
            AttemptCount = 5
        };

        var collection = GetRawMongoCollection<WebhookDeliveryStyleEntity>("order_matrix_string_shadow");
        await collection.InsertOneAsync(entity);

        var rawDoc = await collection.Database.GetCollection<BsonDocument>("order_matrix_string_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();

        rawDoc.Should().NotBeNull();
        // Which status-named elements leaked?
        var statusNames = string.Join(", ", rawDoc.Names.Where(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)));
        statusNames.Should().Be("status",
            "only one status field must exist in the stored document");
        rawDoc["status"].AsString.Should().Be("permanent_failure");
        rawDoc["url"].AsString.Should().Be("https://partner.example.com/webhook");
        rawDoc["attempt_count"].AsInt32.Should().Be(5);
        // Assert no duplicate status element or placeholder leaked into the document.
        rawDoc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1,
            "only one status field must exist in the stored document");

        // Read back via typed collection
        var back = await collection.Find(d => d.Id == entity.Id).FirstOrDefaultAsync();
        back.Should().NotBeNull();
        back!.Status.Should().Be("permanent_failure");
        back.Url.Should().Be("https://partner.example.com/webhook");
        back.AttemptCount.Should().Be(5);
    }

    private async Task AssertStringShadowBehaviorAsync()
    {
        var entity = new WebhookDeliveryStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = "pending",
            Url = "https://hooks.phanes.ltd/events",
            AttemptCount = 1
        };

        var collection = GetRawMongoCollection<WebhookDeliveryStyleEntity>("order_matrix_string_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("order_matrix_string_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsString.Should().Be("pending");

        var filter = Builders<WebhookDeliveryStyleEntity>.Filter.Where(d => d.Status == "pending");
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be("pending");
    }

    private async Task AssertEnumShadowBehaviorAsync()
    {
        var entity = new DpaComplianceStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = DpaStatus.Active,
            AgreementName = "GDPR-Article-28-Standard"
        };

        var collection = GetRawMongoCollection<DpaComplianceStyleEntity>("order_matrix_enum_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("order_matrix_enum_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeFalse("inherited EntityStatus must not be persisted");
        doc.Contains("dpa_status").Should().BeTrue("derived [DbField('dpa_status')] must be used");
        doc["dpa_status"].AsInt32.Should().Be((int)DpaStatus.Active);

        var filter = Builders<DpaComplianceStyleEntity>.Filter.Where(d => d.Status == DpaStatus.Active);
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be(DpaStatus.Active);
    }
}
