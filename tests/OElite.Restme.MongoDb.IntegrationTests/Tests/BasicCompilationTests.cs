using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.Management;
using OElite.Restme.Utils;
using OElite.Restme.Utils.Data;
using Xunit;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Basic integration tests to ensure compilation and basic functionality
/// </summary>
public class BasicCompilationTests : TestBase
{
    [DbCollection("basic_test_products")]
    [DbIndex("idx_product_name", "Name")]
    [DbIndex("idx_product_price", "Price")]
    public class BasicTestProduct : BaseEntity
    {
        [DbField("name")]
        public string Name { get; set; } = string.Empty;

        [DbField("price")]
        public decimal Price { get; set; }

        [DbField("description")]
        public string Description { get; set; } = string.Empty;
    }

    [DbCollection("basic_test_orders", EnableSharding = true)]
    [DbShardKey("CustomerId")]
    [DbIndex("idx_order_customer", "CustomerId")]
    public class BasicTestOrder : BaseEntity
    {
        [DbField("customer_id")]
        public DbObjectId CustomerId { get; set; }

        [DbField("total_amount")]
        public decimal TotalAmount { get; set; }

        [DbField("order_date")]
        public DateTime OrderDate { get; set; }
    }

    [DbCollection("basic_test_region", EnableSharding = true)]
    [DbShardKey("UserId", IncludeRegion = true)]
    [DbIndex("idx_region_user", "Region", "UserId")]
    public class BasicTestRegionData : BaseEntity
    {
        [DbField("user_id")]
        public DbObjectId UserId { get; set; }

        [DbField("data_type")]
        public string DataType { get; set; } = string.Empty;

        [DbField("content")]
        public string Content { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Should_Bootstrap_Basic_Collection_Successfully()
    {
        // Arrange
        var bootstrapService = new DbBootstrapService(DbCentre);
        var entityTypes = new[] { typeof(BasicTestProduct) };

        // Act
        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = false,
            CreateIndexesInBackground = true,
            MaxRetryAttempts = 3
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.ConfiguredCollections.Should().Contain("basic_test_products");
    }

    [Fact]
    public async Task Should_Bootstrap_Sharded_Collection_Successfully()
    {
        // Arrange
        var bootstrapService = new DbBootstrapService(DbCentre);
        var entityTypes = new[] { typeof(BasicTestOrder) };

        // Act
        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 8,
            CreateIndexesInBackground = true,
            MaxRetryAttempts = 3
        });

        // Assert
        result.Should().NotBeNull();


        result.Success.Should().BeTrue();
        result.ConfiguredCollections.Should().Contain("basic_test_orders");
        result.ShardKeysConfigured.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Bootstrap_Region_Aware_Collection_Successfully()
    {
        // Arrange
        var geoConfig = new GeographicConfiguration
        {
            DefaultRegion = "eu"
        };

        var bootstrapService = new DbBootstrapService(DbCentre);
        var entityTypes = new[] { typeof(BasicTestRegionData) };

        // Act
        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 4,
            CreateIndexesInBackground = true,
            GeographicConfiguration = geoConfig
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.ConfiguredCollections.Should().Contain("basic_test_region");
    }

    [Fact]
    public void Should_Create_Database_Collections()
    {
        // Arrange
        var collection = DbCentre.GetMongoDbCollection<BasicTestProduct>("basic_test_products");

        // Act & Assert - Just verify collection can be obtained
        collection.Should().NotBeNull();
    }

    [Fact]
    public void Should_Support_Entity_Attribute_Configuration()
    {
        // Arrange & Act
        var productType = typeof(BasicTestProduct);
        var orderType = typeof(BasicTestOrder);
        var regionType = typeof(BasicTestRegionData);

        // Assert - Verify attributes are properly configured
        var productCollectionAttr = productType.GetCustomAttributes(typeof(DbCollectionAttribute), false);
        var orderShardKeyAttr = orderType.GetCustomAttributes(typeof(DbShardKeyAttribute), false);
        var regionIndexAttr = regionType.GetCustomAttributes(typeof(DbIndexAttribute), false);

        productCollectionAttr.Should().HaveCount(1);
        orderShardKeyAttr.Should().HaveCount(1);
        regionIndexAttr.Should().HaveCount(1);

        var collectionAttr = (DbCollectionAttribute)productCollectionAttr[0];
        collectionAttr.CollectionName.Should().Be("basic_test_products");

        var shardKeyAttr = (DbShardKeyAttribute)orderShardKeyAttr[0];
        shardKeyAttr.Fields.Should().Contain("CustomerId");

        var indexAttr = (DbIndexAttribute)regionIndexAttr[0];
        indexAttr.Name.Should().Be("idx_region_user");
    }

    [Fact]
    public async Task Should_Get_Database_Health_Status()
    {
        // Act
        var healthStatus = await DbCentre.GetHealthStatusAsync();

        // Assert
        healthStatus.Should().NotBeNull();
        healthStatus.IsAccessible.Should().BeTrue();
        healthStatus.DatabaseStatistics.Should().NotBeNull();
    }

    [Fact]
    public void Should_Create_Region_Configuration()
    {
        // Act
        var geoConfig = new GeographicConfiguration
        {
            DefaultRegion = "eu"
        };

        // Assert
        geoConfig.Should().NotBeNull();
        geoConfig.DefaultRegion.Should().Be("eu");
    }

    [Fact]
    public void Should_Create_Bootstrap_Options()
    {
        // Act
        var options = new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 16,
            CreateIndexesInBackground = true,
            MaxRetryAttempts = 5,
            TimeoutSeconds = 300
        };

        // Assert
        options.Should().NotBeNull();
        options.EnableSharding.Should().BeTrue();
        options.PreSplitCount.Should().Be(16);
        options.MaxRetryAttempts.Should().Be(5);
        options.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void Should_Support_Region_Sharding_Strategies()
    {
        // Act & Assert
        var regionFirst = RegionShardingStrategy.RegionFirst;
        var regionMiddle = RegionShardingStrategy.RegionMiddle;
        var regionLast = RegionShardingStrategy.RegionLast;

        regionFirst.Should().Be(RegionShardingStrategy.RegionFirst);
        regionMiddle.Should().Be(RegionShardingStrategy.RegionMiddle);
        regionLast.Should().Be(RegionShardingStrategy.RegionLast);
    }
}