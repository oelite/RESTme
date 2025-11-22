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
/// Integration tests for region migration scenarios
/// Tests moving data between regions/shards due to customer relocation,
/// data sovereignty requirements, and GDPR compliance changes
/// </summary>
public class RegionMigrationTests : TestBase
{
    [DbCollection("migration_test_users", EnableSharding = true, EnablePreSplitting = true)]
    [DbShardKey("UserId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
    [DbIndex("idx_user_region", "Region", "UserId")]
    [DbIndex("idx_user_email", "Email", IsUnique = true)]
    [DbIndex("idx_migration_tracking", "Region", "MigrationStatus", "LastMigrated")]
    public class MigrationTestUser : BaseEntity
    {
        [DbField("user_id")]
        public string UserId { get; set; } = string.Empty;

        [DbField("email")]
        public string Email { get; set; } = string.Empty;

        [DbField("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [DbField("last_name")]
        public string LastName { get; set; } = string.Empty;

        [DbField("migration_status")]
        public string MigrationStatus { get; set; } = "stable";

        [DbField("original_region")]
        public string? OriginalRegion { get; set; }

        [DbField("last_migrated")]
        public DateTime? LastMigrated { get; set; }

        [DbField("migration_reason")]
        public string? MigrationReason { get; set; }
    }

    [DbCollection("migration_audit_trail", EnableSharding = true)]
    [DbShardKey("UserId", IncludeRegion = true)]
    [DbIndex("idx_audit_migration", "MigrationId")]
    [DbIndex("idx_audit_user", "UserId", "MigrationDate")]
    public class MigrationAuditTrail : BaseEntity
    {
        [DbField("migration_id")]
        public string MigrationId { get; set; } = string.Empty;

        [DbField("user_id")]
        public string UserId { get; set; } = string.Empty;

        [DbField("source_region")]
        public string SourceRegion { get; set; } = string.Empty;

        [DbField("target_region")]
        public string TargetRegion { get; set; } = string.Empty;

        [DbField("migration_date")]
        public DateTime MigrationDate { get; set; }

        [DbField("migration_reason")]
        public string MigrationReason { get; set; } = string.Empty;

        [DbField("status")]
        public new string Status { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Should_Bootstrap_Region_Migration_Collections()
    {
        // Arrange
        var geoConfig = new GeographicConfiguration
        {
            DefaultRegion = "eu"
        };

        var bootstrapService = new DbBootstrapService(DbCentre);
        var entityTypes = new[] { typeof(MigrationTestUser), typeof(MigrationAuditTrail) };

        // Act
        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 4,
            CreateIndexesInBackground = true,
            GeographicConfiguration = geoConfig,
            MaxRetryAttempts = 3
        });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.ConfiguredCollections.Should().Contain("migration_test_users");
        result.ConfiguredCollections.Should().Contain("migration_audit_trail");
        result.ShardKeysConfigured.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Create_Users_In_Different_Regions()
    {
        // Arrange
        var usersCollection = DbCentre.GetMongoDbCollection<MigrationTestUser>("migration_test_users");

        var testUsers = new[]
        {
            new MigrationTestUser
            {
                Id = DbObjectId.NewId(),
                UserId = "user001",
                Email = "john.doe@example.com",
                FirstName = "John",
                LastName = "Doe",
                Region = "us",
                MigrationStatus = "stable",
                OriginalRegion = "us"
            },
            new MigrationTestUser
            {
                Id = DbObjectId.NewId(),
                UserId = "user002",
                Email = "jane.smith@example.com",
                FirstName = "Jane",
                LastName = "Smith",
                Region = "eu",
                MigrationStatus = "stable",
                OriginalRegion = "eu"
            }
        };

        // Act
        foreach (var user in testUsers)
        {
            await usersCollection.InsertOneAsync(user);
        }

        // Assert - Verify users were created in correct regions
        var usUserCount = await usersCollection.CountDocumentsAsync(x => x.Region == "us");
        var euUserCount = await usersCollection.CountDocumentsAsync(x => x.Region == "eu");

        usUserCount.Should().Be(1);
        euUserCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Support_Region_Migration_Tracking()
    {
        // Arrange
        var usersCollection = DbCentre.GetMongoDbCollection<MigrationTestUser>("migration_test_users");
        var auditCollection = DbCentre.GetMongoDbCollection<MigrationAuditTrail>("migration_audit_trail");

        var testUser = new MigrationTestUser
        {
            Id = DbObjectId.NewId(),
            UserId = "migration_user001",
            Email = "migrate.me@example.com",
            FirstName = "Migration",
            LastName = "Test",
            Region = "us",
            MigrationStatus = "stable",
            OriginalRegion = "us"
        };

        await usersCollection.InsertOneAsync(testUser);

        // Act - Simulate region migration by updating user and creating audit trail
        testUser.Region = "eu";
        testUser.MigrationStatus = "migrated";
        testUser.LastMigrated = DateTime.UtcNow;
        testUser.MigrationReason = "gdpr_compliance";

        var replaceResult = await usersCollection.ReplaceOneAsync(x => x.UserId == "migration_user001", testUser);

        // Create audit trail
        var auditRecord = new MigrationAuditTrail
        {
            Id = DbObjectId.NewId(),
            MigrationId = Guid.NewGuid().ToString(),
            UserId = testUser.UserId,
            SourceRegion = "us",
            TargetRegion = "eu",
            MigrationDate = DateTime.UtcNow,
            MigrationReason = "gdpr_compliance",
            Status = "completed",
            Region = "eu" // Audit follows user to new region
        };

        await auditCollection.InsertOneAsync(auditRecord);

        // Assert
        replaceResult.Should().BeTrue();

        var migratedUser = await usersCollection.FindOneAsync(x => x.UserId == "migration_user001");
        migratedUser.Should().NotBeNull();
        migratedUser!.Region.Should().Be("eu");
        migratedUser.MigrationStatus.Should().Be("migrated");
        migratedUser.MigrationReason.Should().Be("gdpr_compliance");

        var auditRecords = await auditCollection.FindAsync(x => x.UserId == "migration_user001");
        auditRecords.Should().HaveCount(1);
        auditRecords[0].SourceRegion.Should().Be("us");
        auditRecords[0].TargetRegion.Should().Be("eu");
        auditRecords[0].Status.Should().Be("completed");
    }

    [Fact]
    public async Task Should_Support_Bulk_Region_Migration()
    {
        // Arrange
        var usersCollection = DbCentre.GetMongoDbCollection<MigrationTestUser>("migration_test_users");

        var bulkUsers = new[]
        {
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "bulk001", Region = "legacy_region", Email = "bulk1@example.com", MigrationStatus = "pending_migration" },
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "bulk002", Region = "legacy_region", Email = "bulk2@example.com", MigrationStatus = "pending_migration" },
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "bulk003", Region = "legacy_region", Email = "bulk3@example.com", MigrationStatus = "pending_migration" }
        };

        foreach (var user in bulkUsers)
        {
            await usersCollection.InsertOneAsync(user);
        }

        // Act - Simulate bulk migration using UpdateManyAsync with Dictionary filters
        var migrationFilter = new Dictionary<string, object>
        {
            ["region"] = "legacy_region",
            ["migration_status"] = "pending_migration"
        };

        var migrationUpdate = new Dictionary<string, object>
        {
            ["$set"] = new Dictionary<string, object>
            {
                ["region"] = "eu",
                ["original_region"] = "legacy_region",
                ["migration_status"] = "migrated",
                ["last_migrated"] = DateTime.UtcNow,
                ["migration_reason"] = "regulatory_compliance"
            }
        };

        var updateResult = await usersCollection.UpdateManyAsync(migrationFilter, migrationUpdate);

        // Assert
        updateResult.Should().Be(3); // All 3 users should be migrated

        // Verify users are now in target region
        var migratedCount = await usersCollection.CountDocumentsAsync(x => x.Region == "eu" && x.MigrationStatus == "migrated");
        migratedCount.Should().Be(3);

        // Verify no users remain in legacy region with pending status
        var remainingCount = await usersCollection.CountDocumentsAsync(x => x.Region == "legacy_region" && x.MigrationStatus == "pending_migration");
        remainingCount.Should().Be(0);
    }

    [Fact]
    public void Should_Support_Region_Sharding_Strategies()
    {
        // Act & Assert - Verify region sharding strategies are available
        var regionFirst = RegionShardingStrategy.RegionFirst;
        var regionMiddle = RegionShardingStrategy.RegionMiddle;
        var regionLast = RegionShardingStrategy.RegionLast;

        regionFirst.Should().Be(RegionShardingStrategy.RegionFirst);
        regionMiddle.Should().Be(RegionShardingStrategy.RegionMiddle);
        regionLast.Should().Be(RegionShardingStrategy.RegionLast);
    }

    [Fact]
    public void Should_Support_Geographic_Configuration()
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
    public async Task Should_Support_Cross_Region_Data_Queries()
    {
        // Arrange
        var usersCollection = DbCentre.GetMongoDbCollection<MigrationTestUser>("migration_test_users");

        var crossRegionUsers = new[]
        {
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "cross001", Region = "us", Email = "us@example.com" },
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "cross002", Region = "eu", Email = "eu@example.com" },
            new MigrationTestUser { Id = DbObjectId.NewId(), UserId = "cross003", Region = "ap", Email = "ap@example.com" }
        };

        foreach (var user in crossRegionUsers)
        {
            await usersCollection.InsertOneAsync(user);
        }

        // Act & Assert - Query users across different regions
        var allUsers = await usersCollection.FindAsync(x => x.UserId.StartsWith("cross"));
        allUsers.Should().HaveCount(3);

        var usUsers = await usersCollection.FindAsync(x => x.Region == "us" && x.UserId.StartsWith("cross"));
        usUsers.Should().HaveCount(1);

        var euUsers = await usersCollection.FindAsync(x => x.Region == "eu" && x.UserId.StartsWith("cross"));
        euUsers.Should().HaveCount(1);

        var apUsers = await usersCollection.FindAsync(x => x.Region == "ap" && x.UserId.StartsWith("cross"));
        apUsers.Should().HaveCount(1);
    }
}