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
/// Integration tests for GDPR compliance scenarios including region-aware data handling,
/// data sovereignty requirements, consent management, and cross-border data transfer restrictions
/// </summary>
public class GdprComplianceTests : TestBase
{
    [DbCollection("gdpr_customer_data", EnableSharding = true, EnablePreSplitting = true)]
    [DbShardKey("Email", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
    [DbIndex("idx_region_compliance", "Region", "ConsentStatus", "LastUpdated")]
    [DbIndex("idx_gdpr_email", "Email", IsUnique = true)]
    [DbIndex("idx_consent_tracking", "ConsentStatus", "ConsentDate")]
    [DbIndex("idx_data_retention", "Region", "DataRetentionDate")]
    public class GdprCustomerData : BaseEntity
    {
        [DbField("email")]
        public string Email { get; set; } = string.Empty;

        [DbField("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [DbField("last_name")]
        public string LastName { get; set; } = string.Empty;

        [DbField("consent_status")]
        public string ConsentStatus { get; set; } = "pending";

        [DbField("consent_date")]
        public DateTime? ConsentDate { get; set; }

        [DbField("consent_version")]
        public string ConsentVersion { get; set; } = "1.0";

        [DbField("data_processing_purposes")]
        public List<string> DataProcessingPurposes { get; set; } = new();

        [DbField("data_retention_date")]
        public DateTime? DataRetentionDate { get; set; }

        [DbField("last_access_date")]
        public DateTime? LastAccessDate { get; set; }

        [DbField("marketing_consent")]
        public bool MarketingConsent { get; set; } = false;

        [DbField("profiling_consent")]
        public bool ProfilingConsent { get; set; } = false;

        [DbField("third_party_sharing_consent")]
        public bool ThirdPartySharingConsent { get; set; } = false;
    }

    [DbCollection("gdpr_data_requests", EnableSharding = true)]
    [DbShardKey("Email", IncludeRegion = true)]
    [DbIndex("idx_request_tracking", "RequestType", "RequestStatus", "RequestDate")]
    [DbIndex("idx_customer_requests", "Email", "RequestDate")]
    public class GdprDataRequest : BaseEntity
    {
        [DbField("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [DbField("email")]
        public string Email { get; set; } = string.Empty;

        [DbField("request_type")]
        public string RequestType { get; set; } = string.Empty; // access, portability, erasure, rectification

        [DbField("request_status")]
        public string RequestStatus { get; set; } = "pending";

        [DbField("request_date")]
        public DateTime RequestDate { get; set; }

        [DbField("completion_date")]
        public DateTime? CompletionDate { get; set; }

        [DbField("response_data")]
        public string? ResponseData { get; set; }

        [DbField("legal_basis")]
        public string LegalBasis { get; set; } = string.Empty;

        [DbField("data_categories")]
        public List<string> DataCategories { get; set; } = new();

        [DbField("status")]
        public new string Status { get; set; } = "pending"; // Override BaseEntity.Status to avoid conflicts
    }

    [DbCollection("gdpr_audit_log", EnableSharding = true)]
    [DbShardKey("Email", IncludeRegion = true)]
    [DbIndex("idx_audit_date", "AuditDate")]
    [DbIndex("idx_audit_action", "ActionType", "AuditDate")]
    public class GdprAuditLog : BaseEntity
    {
        [DbField("email")]
        public string Email { get; set; } = string.Empty;

        [DbField("action_type")]
        public string ActionType { get; set; } = string.Empty;

        [DbField("audit_date")]
        public DateTime AuditDate { get; set; }

        [DbField("details")]
        public string Details { get; set; } = string.Empty;

        [DbField("ip_address")]
        public string? IpAddress { get; set; }

        [DbField("user_agent")]
        public string? UserAgent { get; set; }

        [DbField("legal_basis")]
        public string LegalBasis { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Should_Bootstrap_GDPR_Collections_Successfully()
    {
        // Arrange
        var geoConfig = new GeographicConfiguration
        {
            DefaultRegion = "eu"
        };

        var bootstrapService = new DbBootstrapService(DbCentre);
        var entityTypes = new[] { typeof(GdprCustomerData), typeof(GdprDataRequest), typeof(GdprAuditLog) };

        // Act
        var result = await bootstrapService.BootstrapForEntitiesAsync(entityTypes, new DbBootstrapOptions
        {
            EnableSharding = true,
            EnablePreSplitting = true,
            PreSplitCount = 8,
            CreateIndexesInBackground = true,
            GeographicConfiguration = geoConfig,
            MaxRetryAttempts = 3
        });

        // Assert
        result.Should().NotBeNull();
        if (!result.Success)
        {
            var detailedError = $"Bootstrap failed: {result.ErrorMessage}";
            if (result.ValidationResult?.Messages?.Any() == true)
            {
                detailedError += $"\nValidation messages: {string.Join(", ", result.ValidationResult.Messages)}";
            }
            throw new InvalidOperationException(detailedError);
        }
        result.Success.Should().BeTrue();
        result.ConfiguredCollections.Should().Contain("gdpr_customer_data");
        result.ConfiguredCollections.Should().Contain("gdpr_data_requests");
        result.ConfiguredCollections.Should().Contain("gdpr_audit_log");
        result.ShardKeysConfigured.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Handle_Consent_Management_With_Region_Awareness()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");
        var auditCollection = DbCentre.GetMongoDbCollection<GdprAuditLog>("gdpr_audit_log");

        var testCustomers = new[]
        {
            new GdprCustomerData
            {
                Email = "eu.customer@example.com",
                FirstName = "Hans",
                LastName = "Mueller",
                Region = "eu",
                ConsentStatus = "given",
                ConsentDate = DateTime.UtcNow,
                ConsentVersion = "2.0",
                DataProcessingPurposes = new List<string> { "marketing", "analytics" },
                MarketingConsent = true,
                ProfilingConsent = false,
                ThirdPartySharingConsent = false
            },
            new GdprCustomerData
            {
                Email = "us.customer@example.com",
                FirstName = "John",
                LastName = "Smith",
                Region = "us",
                ConsentStatus = "given",
                ConsentDate = DateTime.UtcNow,
                ConsentVersion = "2.0",
                DataProcessingPurposes = new List<string> { "service_delivery" },
                MarketingConsent = false,
                ProfilingConsent = false,
                ThirdPartySharingConsent = false
            }
        };

        // Act
        foreach (var customer in testCustomers)
        {
            await customerCollection.InsertOneAsync(customer);

            // Create audit log for consent
            var auditLog = new GdprAuditLog
            {
                Email = customer.Email,
                ActionType = "consent_given",
                AuditDate = DateTime.UtcNow,
                Details = $"Consent given for purposes: {string.Join(", ", customer.DataProcessingPurposes)}",
                LegalBasis = "consent",
                Region = customer.Region
            };
            await auditCollection.InsertOneAsync(auditLog);
        }

        // Assert
        var euCustomers = await customerCollection.CountDocumentsAsync(x => x.Region == "eu" && x.ConsentStatus == "given");
        var usCustomers = await customerCollection.CountDocumentsAsync(x => x.Region == "us" && x.ConsentStatus == "given");

        euCustomers.Should().Be(1);
        usCustomers.Should().Be(1);

        // Verify audit trails
        var auditRecords = await auditCollection.FindAsync(x => x.ActionType == "consent_given");
        auditRecords.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_Handle_Data_Subject_Access_Requests()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");
        var requestCollection = DbCentre.GetMongoDbCollection<GdprDataRequest>("gdpr_data_requests");

        var testCustomer = new GdprCustomerData
        {
            Email = "sar.request@example.com",
            FirstName = "Maria",
            LastName = "Garcia",
            Region = "eu",
            ConsentStatus = "given",
            ConsentDate = DateTime.UtcNow.AddDays(-30),
            DataProcessingPurposes = new List<string> { "service_delivery", "marketing", "analytics" },
            MarketingConsent = true,
            LastAccessDate = DateTime.UtcNow.AddDays(-5)
        };

        await customerCollection.InsertOneAsync(testCustomer);

        // Act - Create Subject Access Request
        var accessRequest = new GdprDataRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Email = testCustomer.Email,
            RequestType = "access",
            RequestStatus = "pending",
            RequestDate = DateTime.UtcNow,
            LegalBasis = "legitimate_interest",
            DataCategories = new List<string> { "personal_data", "consent_records", "processing_history" },
            Region = testCustomer.Region
        };

        await requestCollection.InsertOneAsync(accessRequest);

        // Simulate processing the request
        var customer = await customerCollection.FindOneAsync(x => x.Email == "sar.request@example.com");
        customer.Should().NotBeNull();

        accessRequest.RequestStatus = "completed";
        accessRequest.CompletionDate = DateTime.UtcNow;
        accessRequest.ResponseData = "Customer data exported successfully";

        var updateResult = await requestCollection.ReplaceOneAsync(x => x.RequestId == accessRequest.RequestId, accessRequest);

        // Assert
        updateResult.Should().BeTrue();

        var completedRequest = await requestCollection.FindOneAsync(x => x.RequestId == accessRequest.RequestId);
        completedRequest.Should().NotBeNull();
        completedRequest!.RequestStatus.Should().Be("completed");
        completedRequest.CompletionDate.Should().NotBeNull();
        completedRequest.ResponseData.Should().Contain("exported successfully");
    }

    [Fact]
    public async Task Should_Handle_Right_To_Erasure_Requests()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");
        var requestCollection = DbCentre.GetMongoDbCollection<GdprDataRequest>("gdpr_data_requests");
        var auditCollection = DbCentre.GetMongoDbCollection<GdprAuditLog>("gdpr_audit_log");

        var testCustomer = new GdprCustomerData
        {
            Email = "erasure.request@example.com",
            FirstName = "Pierre",
            LastName = "Dubois",
            Region = "eu",
            ConsentStatus = "given",
            ConsentDate = DateTime.UtcNow.AddDays(-60),
            DataProcessingPurposes = new List<string> { "marketing" },
            MarketingConsent = true
        };

        await customerCollection.InsertOneAsync(testCustomer);

        // Act - Create Right to Erasure Request
        var erasureRequest = new GdprDataRequest
        {
            RequestId = Guid.NewGuid().ToString(),
            Email = testCustomer.Email,
            RequestType = "erasure",
            RequestStatus = "pending",
            RequestDate = DateTime.UtcNow,
            LegalBasis = "consent_withdrawn",
            DataCategories = new List<string> { "all_personal_data" },
            Region = testCustomer.Region
        };

        await requestCollection.InsertOneAsync(erasureRequest);

        // Simulate processing erasure
        var deletedCount = await customerCollection.DeleteManyAsync(x => x.Email == "erasure.request@example.com");
        deletedCount.Should().Be(1);

        // Update request status
        erasureRequest.RequestStatus = "completed";
        erasureRequest.CompletionDate = DateTime.UtcNow;
        erasureRequest.ResponseData = "All personal data has been permanently deleted";

        var updateResult = await requestCollection.ReplaceOneAsync(x => x.RequestId == erasureRequest.RequestId, erasureRequest);

        // Create audit log for deletion
        var auditLog = new GdprAuditLog
        {
            Email = testCustomer.Email,
            ActionType = "data_deleted",
            AuditDate = DateTime.UtcNow,
            Details = "All personal data permanently deleted upon customer request",
            LegalBasis = "right_to_erasure",
            Region = testCustomer.Region
        };
        await auditCollection.InsertOneAsync(auditLog);

        // Assert
        updateResult.Should().BeTrue();

        var verifyDeleted = await customerCollection.FindOneAsync(x => x.Email == "erasure.request@example.com");
        verifyDeleted.Should().BeNull();

        var completedRequest = await requestCollection.FindOneAsync(x => x.RequestId == erasureRequest.RequestId);
        completedRequest.Should().NotBeNull();
        completedRequest!.RequestStatus.Should().Be("completed");

        var auditRecords = await auditCollection.FindAsync(x => x.ActionType == "data_deleted");
        auditRecords.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Enforce_Data_Sovereignty_Restrictions()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");

        var testCustomers = new[]
        {
            new GdprCustomerData
            {
                Email = "eu.restricted@example.com",
                FirstName = "Anna",
                LastName = "Schmidt",
                Region = "eu",
                ConsentStatus = "given",
                DataProcessingPurposes = new List<string> { "service_delivery" },
                ThirdPartySharingConsent = false // No consent for cross-border transfers
            },
            new GdprCustomerData
            {
                Email = "us.customer@example.com",
                FirstName = "Michael",
                LastName = "Johnson",
                Region = "us",
                ConsentStatus = "given",
                DataProcessingPurposes = new List<string> { "service_delivery", "analytics" },
                ThirdPartySharingConsent = true // Consent for transfers
            }
        };

        foreach (var customer in testCustomers)
        {
            await customerCollection.InsertOneAsync(customer);
        }

        // Act & Assert - Query customers by data sovereignty restrictions
        var euRestrictedCustomers = await customerCollection.FindAsync(x =>
            x.Region == "eu" && x.ThirdPartySharingConsent == false);
        euRestrictedCustomers.Should().HaveCount(1);
        euRestrictedCustomers[0].Email.Should().Be("eu.restricted@example.com");

        var transferAllowedCustomers = await customerCollection.FindAsync(x =>
            x.ThirdPartySharingConsent == true);
        transferAllowedCustomers.Should().HaveCount(1);
        transferAllowedCustomers[0].Email.Should().Be("us.customer@example.com");

        // Verify regional isolation
        var euCustomers = await customerCollection.CountDocumentsAsync(x => x.Region == "eu");
        var usCustomers = await customerCollection.CountDocumentsAsync(x => x.Region == "us");

        euCustomers.Should().Be(1);
        usCustomers.Should().Be(1);
    }

    [Fact]
    public async Task Should_Handle_Data_Retention_Policies()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");

        var testCustomers = new[]
        {
            new GdprCustomerData
            {
                Email = "retention.expired@example.com",
                FirstName = "Luigi",
                LastName = "Rossi",
                Region = "eu",
                ConsentStatus = "given",
                ConsentDate = DateTime.UtcNow.AddYears(-3),
                DataRetentionDate = DateTime.UtcNow.AddDays(-30), // Expired
                LastAccessDate = DateTime.UtcNow.AddYears(-1)
            },
            new GdprCustomerData
            {
                Email = "retention.active@example.com",
                FirstName = "Emma",
                LastName = "Wilson",
                Region = "eu",
                ConsentStatus = "given",
                ConsentDate = DateTime.UtcNow.AddMonths(-6),
                DataRetentionDate = DateTime.UtcNow.AddYears(1), // Still valid
                LastAccessDate = DateTime.UtcNow.AddDays(-10)
            }
        };

        foreach (var customer in testCustomers)
        {
            await customerCollection.InsertOneAsync(customer);
        }

        // Act - Identify expired data for deletion
        var expiredDataFilter = new Dictionary<string, object>
        {
            ["data_retention_date"] = new Dictionary<string, object>
            {
                ["$lt"] = DateTime.UtcNow
            }
        };

        var expiredCustomers = await customerCollection.FindAsync(expiredDataFilter);
        expiredCustomers.Should().HaveCount(1);
        expiredCustomers[0].Email.Should().Be("retention.expired@example.com");

        // Simulate automated deletion of expired data
        var deletedCount = await customerCollection.DeleteManyAsync(expiredDataFilter);
        deletedCount.Should().Be(1);

        // Assert - Verify only active data remains
        var activeCustomers = await customerCollection.FindAsync(x => x.Email.Contains("retention"));
        activeCustomers.Should().HaveCount(1);
        activeCustomers[0].Email.Should().Be("retention.active@example.com");
    }

    [Fact]
    public async Task Should_Support_Consent_Withdrawal_And_Data_Migration()
    {
        // Arrange
        var customerCollection = DbCentre.GetMongoDbCollection<GdprCustomerData>("gdpr_customer_data");
        var auditCollection = DbCentre.GetMongoDbCollection<GdprAuditLog>("gdpr_audit_log");

        var testCustomer = new GdprCustomerData
        {
            Email = "consent.withdrawal@example.com",
            FirstName = "Sofia",
            LastName = "Andersson",
            Region = "eu",
            ConsentStatus = "given",
            ConsentDate = DateTime.UtcNow.AddDays(-90),
            DataProcessingPurposes = new List<string> { "marketing", "analytics", "service_delivery" },
            MarketingConsent = true,
            ProfilingConsent = true,
            ThirdPartySharingConsent = true
        };

        await customerCollection.InsertOneAsync(testCustomer);

        // Act - Simulate consent withdrawal
        testCustomer.ConsentStatus = "withdrawn";
        testCustomer.MarketingConsent = false;
        testCustomer.ProfilingConsent = false;
        testCustomer.ThirdPartySharingConsent = false;
        testCustomer.DataProcessingPurposes = new List<string> { "service_delivery" }; // Only essential processing

        var updateResult = await customerCollection.ReplaceOneAsync(x => x.Email == "consent.withdrawal@example.com", testCustomer);

        // Create audit log
        var auditLog = new GdprAuditLog
        {
            Email = testCustomer.Email,
            ActionType = "consent_withdrawn",
            AuditDate = DateTime.UtcNow,
            Details = "Marketing and profiling consent withdrawn, data processing limited to service delivery only",
            LegalBasis = "legitimate_interest",
            Region = testCustomer.Region
        };
        await auditCollection.InsertOneAsync(auditLog);

        // Assert
        updateResult.Should().BeTrue();

        var updatedCustomer = await customerCollection.FindOneAsync(x => x.Email == "consent.withdrawal@example.com");
        updatedCustomer.Should().NotBeNull();
        updatedCustomer!.ConsentStatus.Should().Be("withdrawn");
        updatedCustomer.MarketingConsent.Should().BeFalse();
        updatedCustomer.ProfilingConsent.Should().BeFalse();
        updatedCustomer.DataProcessingPurposes.Should().Contain("service_delivery");
        updatedCustomer.DataProcessingPurposes.Should().NotContain("marketing");
        updatedCustomer.DataProcessingPurposes.Should().NotContain("analytics");

        var auditRecords = await auditCollection.FindAsync(x => x.ActionType == "consent_withdrawn");
        auditRecords.Should().HaveCount(1);
    }
}