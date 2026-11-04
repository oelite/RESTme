using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb;
using Xunit;

namespace OElite.Restme.UnitTests;

/// <summary>
/// CI-runnable regression guard for restme #22 (oelite/uranus/restme#22).
///
/// Reproduces the #1564 Cat 3 alias-collision entity shape from origin-auth #1732
/// (MfaDevice.DeviceState / PushNotificationLog.DeliveryState / PushNotificationConfig.ConfigState):
/// a BaseEntity-derived type that BOTH shadows <see cref="BaseEntity.Status"/> with
/// <c>new</c> AND declares a separate computed alias property mapped to the SAME
/// Bson element name. On 2.1.1-develop.15334 (regression introduced by MR !11 /
/// issue #12) constructing <see cref="ShadowedStatusSerializer{T}"/> threw
/// BsonSerializationException (duplicate element) at class-map Freeze.
///
/// These tests need NO MongoDB server — they exercise only the in-memory class-map
/// freeze, serializer resolution, BSON round-trip, and LINQ3 filter rendering, so they
/// run in CI (Category != Integration) and guard the regression at the pipeline gate.
/// </summary>
[Trait("Category", "Unit")]
public class ShadowedStatusAliasCollisionUnitTests
{
    [DbCollection("unit_alias_string_shadow")]
    public class StringShadowWithAliasEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";

        // #1564 Cat 3 computed alias mapped to the SAME element name as the shadow.
        [DbField("status")]
        public string DeliveryState
        {
            get => Status;
            set => Status = value;
        }

        public string Url { get; set; } = string.Empty;
    }

    [DbCollection("unit_alias_enum_shadow")]
    public class EnumShadowWithAliasEntity : BaseEntity
    {
        [DbField("device_status")]
        public new DeviceShadowStatus Status { get; set; } = DeviceShadowStatus.Active;

        [DbField("device_status")]
        public DeviceShadowStatus DeviceState
        {
            get => Status;
            set => Status = value;
        }
    }

    // Baseline #12 shape — shadow with explicit [DbField], NO alias. Must stay unchanged.
    [DbCollection("unit_alias_no_alias")]
    public class NoAliasShadowEntity : BaseEntity
    {
        [DbField("status")]
        public new string Status { get; set; } = "pending";
    }

    public enum DeviceShadowStatus
    {
        Active = 0,
        Disabled = 1,
        Removed = 3
    }

    [Fact]
    public void SerializerConstruction_StringShadowWithAlias_FreezeSucceeds()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        // On the defective 15334 build this throws BsonSerializationException:
        // duplicate element name 'status'.
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<StringShadowWithAliasEntity>();
        serializer.Should().NotBeNull();
        serializer.Should().BeOfType<ShadowedStatusSerializer<StringShadowWithAliasEntity>>();
    }

    [Fact]
    public void SerializerConstruction_EnumShadowWithAlias_FreezeSucceeds()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<EnumShadowWithAliasEntity>();
        serializer.Should().NotBeNull();
        serializer.Should().BeOfType<ShadowedStatusSerializer<EnumShadowWithAliasEntity>>();
    }

    [Fact]
    public void BsonRoundTrip_StringShadowWithAlias_SingleStatusElementViaAlias()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        var entity = new StringShadowWithAliasEntity
        {
            Id = DbObjectId.NewId(),
            Status = "permanent_failure",
            Url = "https://partner.example.com/webhook"
        };

        var doc = entity.ToBsonDocument();

        // Alias owns the shared element: exactly ONE status-named element, no phantom
        // inherited EntityStatus element.
        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1,
            "alias and shadow must map to a single status element, not two");
        doc.Contains("status").Should().BeTrue();
        doc["status"].AsString.Should().Be("permanent_failure");

        var roundTripped = BsonSerializer.Deserialize<StringShadowWithAliasEntity>(doc);
        roundTripped.Status.Should().Be("permanent_failure");
        roundTripped.DeliveryState.Should().Be("permanent_failure");
        roundTripped.Url.Should().Be("https://partner.example.com/webhook");
    }

    [Fact]
    public void BsonRoundTrip_EnumShadowWithAlias_SingleDeviceStatusElementViaAlias()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        var entity = new EnumShadowWithAliasEntity
        {
            Id = DbObjectId.NewId(),
            Status = DeviceShadowStatus.Removed
        };

        var doc = entity.ToBsonDocument();

        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        doc.Contains("device_status").Should().BeTrue();
        doc["device_status"].AsInt32.Should().Be((int)DeviceShadowStatus.Removed);

        var roundTripped = BsonSerializer.Deserialize<EnumShadowWithAliasEntity>(doc);
        roundTripped.Status.Should().Be(DeviceShadowStatus.Removed);
        roundTripped.DeviceState.Should().Be(DeviceShadowStatus.Removed);
    }

    [Fact]
    public void NoAliasShadow_ExplicitDbFieldElementName_Preserved()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        var entity = new NoAliasShadowEntity { Id = DbObjectId.NewId(), Status = "sent" };

        var doc = entity.ToBsonDocument();
        doc.Contains("status").Should().BeTrue("no alias exists — explicit [DbField] name preserved (issue #12 contract)");
        doc["status"].AsString.Should().Be("sent");
        doc.Names.Count(n => n.Contains("status", StringComparison.OrdinalIgnoreCase)).Should().Be(1);

        var roundTripped = BsonSerializer.Deserialize<NoAliasShadowEntity>(doc);
        roundTripped.Status.Should().Be("sent");
    }

    [Fact]
    public void FilterRender_AliasAndShadowMembers_BindToSharedElement()
    {
        MongoClassMapConfigurator.ResetForTesting();
        MongoClassMapConfigurator.ConfigureClassMappings();

        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<StringShadowWithAliasEntity>();

        // Filter on the SHADOW member — must bind through the alias to the shared element.
        var shadowFilter = Builders<StringShadowWithAliasEntity>.Filter.Where(d => d.Status == "failed");
        var shadowJson = shadowFilter.Render(new RenderArgs<StringShadowWithAliasEntity>(
            serializer, BsonSerializer.SerializerRegistry)).ToString();
        shadowJson.Should().Contain("\"status\"");
        shadowJson.Should().Contain("\"failed\"");

        // Filter on the ALIAS member — the origin-auth repositories' query path.
        var aliasFilter = Builders<StringShadowWithAliasEntity>.Filter.Where(d => d.DeliveryState == "pending");
        var aliasJson = aliasFilter.Render(new RenderArgs<StringShadowWithAliasEntity>(
            serializer, BsonSerializer.SerializerRegistry)).ToString();
        aliasJson.Should().Contain("\"status\"");
        aliasJson.Should().Contain("\"pending\"");
    }
}
