using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Failing-first regression suite for restme #22 (oelite/uranus/restme#22), the regression
/// introduced by MR !11 / issue #12 (shipped in 2.1.1-develop.15334) in the #1564 Cat 3
/// pattern: a BaseEntity-derived entity that BOTH shadows <see cref="BaseEntity.Status"/>
/// with <c>new</c> AND declares a separate alias property mapped to the SAME Bson element
/// name.
///
/// <para>
/// The exact origin-auth shape (verified in origin-auth #1732 / MR !1235):
/// <code>
/// [DbField("config_status")]
/// public new ConfigStatus Status { get; set; }
///
/// [DbField("config_status")]   // same element name as the shadow
/// public ConfigStatus ConfigState
/// {
///     get => Status;
///     set => Status = value;
/// }
/// </code>
/// MfaDevice.DeviceState / PushNotificationLog.DeliveryState / PushNotificationConfig.ConfigState.
/// On 2.1.1-develop.9359 these passed; on 15334 the private class map freeze in
/// <see cref="ShadowedStatusSerializer{T}"/> threw a BsonSerializationException because the
/// shadowed Status and the alias both resolved to the same element name.
///
/// <para>
/// Semantics restored (pre-9359, "alias wins for the shared element"): the shadowed
/// Status is renamed to the &lt;type&gt;_status prefixed name and the alias keeps the shared
/// element name. The document stores the value once under the alias element; the shadow
/// round-trips through the alias (computed accessor). This preserves all #12 ACs:
/// order independence, non-shadow entities byte-identical, and LINQ3 binding on both members.
/// </para>
/// </summary>
[Collection("Status class-map registry")]
public class ShadowedStatusAliasCollisionTest : TestBase
{
    [DbCollection("alias_matrix_string_shadow")]
    public class WebhookLogStyleEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";

        // #1564 Cat 3 computed alias, mapped to the SAME element name as the shadow.
        [DbField("status")]
        public string DeliveryState
        {
            get => Status;
            set => Status = value;
        }

        public string Url { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
    }

    [DbCollection("alias_matrix_enum_shadow")]
    public class MfaDeviceStyleEntity : BaseEntity
    {
        [DbField("device_status")]
        public new MfaDeviceStatus Status { get; set; } = MfaDeviceStatus.Active;

        // #1564 Cat 3 computed alias, mapped to the SAME element name as the shadow.
        [DbField("device_status")]
        public MfaDeviceStatus DeviceState
        {
            get => Status;
            set => Status = value;
        }

        public string UserId { get; set; } = string.Empty;
        public bool NotificationsEnabled { get; set; } = true;
    }

    // No-alias shadow — the baseline #12 behavior that MUST NOT change.
    [DbCollection("alias_matrix_no_alias")]
    public class NoAliasShadowEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";
        public string Name { get; set; } = string.Empty;
    }

    // Non-shadow guard — inherited EntityStatus must persist unchanged (byte-identical).
    [DbCollection("alias_matrix_non_shadow")]
    public class PlainStyleEntity : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
    }

    public enum MfaDeviceStatus
    {
        Active = 0,
        Disabled = 1,
        TokenExpired = 2,
        Removed = 3,
        Blocked = 4
    }

    // --- CASE 1: String-shadow + alias (registered FIRST) — freeze must not throw, round-trip. ---
    [Fact]
    public async Task Case1_StringShadowWithAlias_RegisteredFirst_FreezeSucceedsAndRoundTrips()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<WebhookLogStyleEntity>();

        await AssertStringShadowWithAliasAsync();
    }

    // --- CASE 2: String-shadow + alias, registered AFTER an unrelated non-shadow entity. ---
    [Fact]
    public async Task Case2_StringShadowWithAlias_RegisteredAfterNonShadow_FreezeSucceedsAndRoundTrips()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<PlainStyleEntity>(); // freeze base first
        MongoClassMapConfigurator.RegisterClassMapping<WebhookLogStyleEntity>();

        await AssertStringShadowWithAliasAsync();
    }

    // --- CASE 3: Enum-shadow + alias (MfaDevice shape) — freeze must not throw, round-trip. ---
    [Fact]
    public async Task Case3_EnumShadowWithAlias_FreezeSucceedsAndRoundTrips()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<MfaDeviceStyleEntity>();

        await AssertEnumShadowWithAliasAsync();
    }

    // --- CASE 4: No-alias shadow baseline — element name unchanged (must not regress #12). ---
    [Fact]
    public async Task Case4_NoAliasShadow_PreservesExplicitDbFieldElementName()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<NoAliasShadowEntity>();

        var entity = new NoAliasShadowEntity { Id = DbObjectId.NewId(), Status = "sent", Name = "baseline" };

        var collection = GetRawMongoCollection<NoAliasShadowEntity>("alias_matrix_no_alias");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("alias_matrix_no_alias")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        // No alias exists → the explicit [DbField("status")] name is preserved (no rename).
        doc["status"].AsString.Should().Be("sent");
        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    // --- CASE 5: Non-shadow entity byte-identical (inherited EntityStatus). ---
    [Fact]
    public async Task Case5_NonShadow_PersistsInheritedEntityStatus_Untouched()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        // Alias-shadow entity registered BEFORE non-shadow to prove no base-map side effects.
        MongoClassMapConfigurator.RegisterClassMapping<WebhookLogStyleEntity>();
        MongoClassMapConfigurator.RegisterClassMapping<PlainStyleEntity>();

        var entity = new PlainStyleEntity
        {
            Id = DbObjectId.NewId(),
            Title = "Baseline Product",
            Status = EntityStatus.Active
        };

        var collection = GetRawMongoCollection<PlainStyleEntity>("alias_matrix_non_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("alias_matrix_non_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsInt32.Should().Be((int)EntityStatus.Active);

        var filter = Builders<PlainStyleEntity>.Filter.Where(p => p.Status == EntityStatus.Active);
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Status.Should().Be(EntityStatus.Active);
    }

    // --- CASE 6: Alias-based query filter (LINQ3 binding) on both members. ---
    [Fact]
    public async Task Case6_AliasAndShadow_BothBindToSameElement_InQueryFilters()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<MfaDeviceStyleEntity>();

        var entity = new MfaDeviceStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = MfaDeviceStatus.Removed,
            UserId = "user-1",
            NotificationsEnabled = true
        };

        var collection = GetRawMongoCollection<MfaDeviceStyleEntity>("alias_matrix_enum_shadow");
        await collection.InsertOneAsync(entity);

        // Filter through the ALIAS (the origin-auth repositories' actual query path).
        var query = DbCentre.GetQuery<MfaDeviceStyleEntity>();
        query.Query(d => d.DeviceState == MfaDeviceStatus.Removed);
        var found = await query.FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Id.Should().Be(entity.Id);
        found.Status.Should().Be(MfaDeviceStatus.Removed);
    }

    // --- CASE 7: Multi-clause Obelisk-shaped Where on the shadowed Status (LINQ3 render). ---
    [Fact]
    public async Task Case7_MultiClauseWhereOnShadow_TranslatesAndMatches()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();
        MongoClassMapConfigurator.RegisterClassMapping<WebhookLogStyleEntity>();

        var entity = new WebhookLogStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = "failed",
            Url = "https://hooks.phanes.ltd/events",
            AttemptCount = 3
        };

        var collection = GetRawMongoCollection<WebhookLogStyleEntity>("alias_matrix_string_shadow");
        await collection.InsertOneAsync(entity);

        var filter = Builders<WebhookLogStyleEntity>.Filter.Where(
            d => d.Status == "pending" || d.Status == "failed");
        var found = await collection.Find(filter).FirstOrDefaultAsync();
        found.Should().NotBeNull();
        found!.Id.Should().Be(entity.Id);
        found.Status.Should().Be("failed");
    }

    private async Task AssertStringShadowWithAliasAsync()
    {
        var entity = new WebhookLogStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = "permanent_failure",
            Url = "https://partner.example.com/webhook",
            AttemptCount = 5
        };

        var collection = GetRawMongoCollection<WebhookLogStyleEntity>("alias_matrix_string_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("alias_matrix_string_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();

        // The shared element 'status' is held by the ALIAS; there is exactly ONE status-named
        // element and it carries the string value. No phantom inherited EntityStatus element.
        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1,
            "alias and shadow must map to a single status element, not two");
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsString.Should().Be("permanent_failure");

        // Round-trip back through the typed collection (shadow reads through the alias).
        var back = await collection.Find(d => d.Id == entity.Id).FirstOrDefaultAsync();
        back.Should().NotBeNull();
        back!.Status.Should().Be("permanent_failure");
        back.DeliveryState.Should().Be("permanent_failure");
        back.Url.Should().Be("https://partner.example.com/webhook");
        back.AttemptCount.Should().Be(5);
    }

    private async Task AssertEnumShadowWithAliasAsync()
    {
        var entity = new MfaDeviceStyleEntity
        {
            Id = DbObjectId.NewId(),
            Status = MfaDeviceStatus.Blocked,
            UserId = "user-42",
            NotificationsEnabled = true
        };

        var collection = GetRawMongoCollection<MfaDeviceStyleEntity>("alias_matrix_enum_shadow");
        await collection.InsertOneAsync(entity);

        var doc = await collection.Database.GetCollection<BsonDocument>("alias_matrix_enum_shadow")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(entity.Id.ToString())))
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();

        // The shared element 'device_status' is held by the ALIAS; single status-named element.
        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1,
            "alias and shadow must map to a single device_status element, not two");
        doc.Contains("device_status").Should().BeTrue();
        doc["device_status"].AsInt32.Should().Be((int)MfaDeviceStatus.Blocked);

        // Round-trip.
        var back = await collection.Find(d => d.Id == entity.Id).FirstOrDefaultAsync();
        back.Should().NotBeNull();
        back!.Status.Should().Be(MfaDeviceStatus.Blocked);
        back.DeviceState.Should().Be(MfaDeviceStatus.Blocked);
        back.UserId.Should().Be("user-42");
    }
}