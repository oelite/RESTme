# Region-Aware Sharding & GDPR Compliance

This document provides comprehensive guidance on implementing region-aware sharding and GDPR compliance using OElite.Restme.MongoDb.

## Table of Contents

- [Overview](#overview)
- [Geographic Data Sovereignty](#geographic-data-sovereignty)
- [Region-Aware Sharding Strategies](#region-aware-sharding-strategies)
- [Attribute-Based Configuration](#attribute-based-configuration)
- [Geographic Data Management](#geographic-data-management)
- [Migration & Compliance](#migration--compliance)
- [Best Practices](#best-practices)
- [Examples](#examples)

## Overview

The OElite.Restme.MongoDb library provides comprehensive support for geographic data management, ensuring compliance with GDPR, CCPA, and other regional data protection regulations through intelligent sharding and automatic data placement.

### Key Features

- **Geographic Data Isolation**: Automatic region-based data placement
- **GDPR/CCPA Compliance**: Built-in support for major data protection regulations
- **Zone Sharding**: MongoDB zone sharding for geographic data sovereignty
- **Cross-Region Migration**: Tools for compliant data movement
- **Retention Policies**: Configurable data retention per region
- **Compliance Validation**: Pre-migration compliance checks

## Geographic Data Sovereignty

### BaseEntity Region Support

All entities automatically inherit region awareness through the enhanced `BaseEntity` class:

```csharp
public class Customer : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Region field automatically inherited from BaseEntity
    // Controls geographic data placement for GDPR compliance
    // public string? Region { get; set; } // from BaseEntity
}

// Entity automatically placed in correct geographic zone
var customer = new Customer
{
    Email = "user@example.com",
    FirstName = "John",
    LastName = "Doe",
    Region = "EU" // Ensures data stays in European jurisdiction
};
```

### Supported Geographic Regions

| Region Code | Description | Legal Framework | Compliance Focus |
|-------------|-------------|-----------------|------------------|
| **EU** | European Union | GDPR | Data protection, consent management |
| **US** | United States | CCPA/CPRA | Privacy rights, data deletion |
| **UK** | United Kingdom | UK GDPR | Post-Brexit data protection |
| **CA** | Canada | PIPEDA | Personal information protection |
| **APAC** | Asia-Pacific | Various | Local data residency laws |
| **CN** | China | Cybersecurity Law | Data localization requirements |

## Region-Aware Sharding Strategies

### 1. Region-First Sharding (Recommended for GDPR)

Places region as the first field in the shard key for optimal data isolation:

```csharp
[DbCollection("customer_data", EnableSharding = true)]
[DbShardKey("UserId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
public class CustomerData : BaseEntity
{
    public DbObjectId UserId { get; set; }
    public string PersonalData { get; set; } = string.Empty;
    public string SensitiveInformation { get; set; } = string.Empty;

    // Effective shard key: { region: 1, user_id: 1 }
    // Ensures all EU data is in EU shards, US data in US shards, etc.
}
```

**Benefits:**
- **Perfect Geographic Isolation**: All data for a region stays on region-specific shards
- **GDPR Compliance**: Simplifies data subject requests and "right to be forgotten"
- **Regulatory Audits**: Easy to demonstrate data residency compliance
- **Zone-Based Queries**: Queries with region filter only hit relevant shards

**Use Cases:**
- Customer personal data (PII)
- Financial records
- Healthcare information
- Any data subject to strict residency requirements

### 2. Region-Last Sharding (Performance Optimized)

Places region at the end for better distribution while maintaining compliance:

```csharp
[DbCollection("product_analytics", EnableSharding = true)]
[DbShardKey("ProductId", "EventDate", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionLast)]
public class ProductAnalytics : BaseEntity
{
    public DbObjectId ProductId { get; set; }
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Dictionary<string, object> EventData { get; set; } = new();

    // Effective shard key: { product_id: 1, event_date: 1, region: 1 }
    // Better distribution for global products with regional compliance
}
```

**Benefits:**
- **Better Performance**: More even data distribution across shards
- **Global Product Support**: Products can be analyzed across regions
- **Temporal Queries**: Time-based queries perform better
- **Compliance Maintained**: Region awareness preserved for regulatory needs

**Use Cases:**
- Analytics and metrics data
- Product usage statistics
- Non-personal behavioral data
- Global content management

### 3. Region-Middle Sharding (Balanced Approach)

Places region in the middle for specific use cases:

```csharp
[DbCollection("order_tracking", EnableSharding = true)]
[DbShardKey("CustomerId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionMiddle, "OrderDate")]
public class OrderTracking : BaseEntity
{
    public DbObjectId CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public decimal OrderValue { get; set; }

    // Effective shard key: { customer_id: 1, region: 1, order_date: 1 }
    // Optimal for customer-centric data with regional compliance
}
```

**Benefits:**
- **Customer-Centric**: Groups data by customer first
- **Regional Compliance**: Maintains geographic boundaries
- **Temporal Ordering**: Time-based queries within customer/region
- **Balanced Distribution**: Avoids hotspots while maintaining compliance

**Use Cases:**
- Order management systems
- Customer journey tracking
- Transaction histories
- User activity logs

## Attribute-Based Configuration

### DbShardKeyAttribute with Region Awareness

The enhanced `DbShardKeyAttribute` provides comprehensive region-aware configuration:

```csharp
public class DbShardKeyAttribute : Attribute
{
    public string[] Fields { get; set; }
    public bool[] IsHashed { get; set; }
    public int[] Directions { get; set; }
    public bool IsUnique { get; set; }

    // Region-aware properties
    public bool IncludeRegion { get; set; } = false;
    public RegionShardingStrategy RegionStrategy { get; set; } = RegionShardingStrategy.RegionFirst;

    // Factory methods for common patterns
    public static DbShardKeyAttribute ForGdprCompliance(string primaryField)
    {
        return new DbShardKeyAttribute(primaryField)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst
        };
    }

    public static DbShardKeyAttribute ForTenant(string tenantField, params string[] additionalFields)
    {
        var fields = new[] { tenantField }.Concat(additionalFields).ToArray();
        return new DbShardKeyAttribute(fields)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionFirst
        };
    }

    public static DbShardKeyAttribute ForAnalytics(string entityField, string timeField)
    {
        return new DbShardKeyAttribute(entityField, timeField)
        {
            IncludeRegion = true,
            RegionStrategy = RegionShardingStrategy.RegionLast
        };
    }
}
```

### Advanced Configuration Examples

#### GDPR-Compliant Customer Entity

```csharp
[DbCollection("gdpr_customers", EnableSharding = true, ValidateSchema = true)]
[DbShardKey("Email", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_customer_lookup", "Email", "Region", IsUnique = true)]
[DbIndex("idx_consent_tracking", "Region", "ConsentDate", "ConsentStatus")]
[DbIndex("idx_data_retention", "Region", "CreatedOnUtc", TtlExpirationSeconds = 94608000)] // 3 years
[DbIndex("idx_erasure_requests", "Region", "ErasureRequestDate", IsSparse = true)]
public class GdprCustomer : BaseEntity
{
    [DbField("email")]
    public string Email { get; set; } = string.Empty;

    [DbField("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [DbField("last_name")]
    public string LastName { get; set; } = string.Empty;

    [DbField("consent_date")]
    public DateTime ConsentDate { get; set; }

    [DbField("consent_status")]
    public string ConsentStatus { get; set; } = string.Empty; // "granted", "withdrawn", "expired"

    [DbField("data_processing_purposes")]
    public List<string> DataProcessingPurposes { get; set; } = new();

    [DbField("erasure_request_date")]
    public DateTime? ErasureRequestDate { get; set; }

    [DbField("data_portability_requests")]
    public List<DataPortabilityRequest> PortabilityRequests { get; set; } = new();

    // Region inherited from BaseEntity ensures geographic compliance
}

public class DataPortabilityRequest
{
    public DateTime RequestDate { get; set; }
    public string RequestStatus { get; set; } = string.Empty; // "pending", "completed", "rejected"
    public string ExportFormat { get; set; } = string.Empty; // "json", "xml", "csv"
    public DateTime? CompletedDate { get; set; }
}
```

#### Q1 S3 Storage with Region Awareness

```csharp
[DbCollection("edge_objects", EnableSharding = true, EnablePreSplitting = true, PreSplitChunks = 1024)]
[DbShardKey("Bucket", "KeyHash", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_object_lookup", "Bucket", "Key", "Region", IsUnique = true)]
[DbIndex("idx_region_listing", "Region", "Bucket", "LastModified")]
[DbIndex("idx_tenant_objects", "OwnerId", "Region", "Bucket", IsSparse = true)]
[DbIndex("idx_compliance_audit", "Region", "ComplianceStatus", "LastAuditDate")]
public class Q1Object : BaseEntity
{
    [DbField("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [DbField("key")]
    public string Key { get; set; } = string.Empty;

    [DbField("key_hash")]
    public string KeyHash { get; set; } = string.Empty;

    [DbField("size")]
    public long Size { get; set; }

    [DbField("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [DbField("owner_id")]
    public DbObjectId? OwnerId { get; set; }

    [DbField("compliance_status")]
    public string ComplianceStatus { get; set; } = "compliant"; // "compliant", "pending_review", "violation"

    [DbField("last_audit_date")]
    public DateTime? LastAuditDate { get; set; }

    [DbField("data_classification")]
    public string DataClassification { get; set; } = "public"; // "public", "internal", "confidential", "restricted"

    // Region field ensures objects are stored in correct geographic zone
    // for compliance with local data residency requirements
}
```

## Geographic Data Management

### GeographicConfiguration

#### Creating GDPR-Compliant Configuration

```csharp
public static GeographicConfiguration CreateGdprCompliantConfiguration()
{
    return new GeographicConfiguration
    {
        DefaultRegion = "EU",
        AllowCrossRegionMigration = true,
        MigrationStrategy = RegionMigrationStrategy.CopyAndArchive,
        MigrationRetentionPeriod = TimeSpan.FromDays(365),
        Regions = new List<RegionConfiguration>
        {
            // European Union - GDPR
            new()
            {
                RegionId = "EU",
                DataCenters = new[] { "eu-west-1", "eu-central-1", "eu-north-1" },
                Jurisdiction = new DataJurisdiction
                {
                    LegalFramework = "GDPR",
                    TransferRestrictions = new[]
                    {
                        "No non-EU transfers without adequacy decision",
                        "Requires explicit consent for third country transfers",
                        "Standard contractual clauses required for non-adequate countries"
                    },
                    RequiresEncryptionAtRest = true,
                    RequiresEncryptionInTransit = true,
                    DataProcessorAgreementRequired = true
                },
                RetentionPolicy = new DataRetentionPolicy
                {
                    DefaultRetentionPeriod = TimeSpan.FromDays(1095), // 3 years
                    PurgeDeletedDataAfter = TimeSpan.FromDays(30),
                    RequiresLegalBasisForRetention = true
                },
                ComplianceRequirements = new[]
                {
                    "Article 7 - Consent management",
                    "Article 17 - Right to erasure",
                    "Article 20 - Right to data portability",
                    "Article 25 - Data protection by design",
                    "Article 32 - Security of processing"
                }
            },

            // United States - CCPA/CPRA
            new()
            {
                RegionId = "US",
                DataCenters = new[] { "us-east-1", "us-west-2", "us-central-1" },
                Jurisdiction = new DataJurisdiction
                {
                    LegalFramework = "CCPA/CPRA",
                    TransferRestrictions = new[]
                    {
                        "No EU transfers without GDPR compliance",
                        "State-specific privacy laws may apply"
                    },
                    RequiresEncryptionAtRest = true,
                    RequiresEncryptionInTransit = true
                },
                RetentionPolicy = new DataRetentionPolicy
                {
                    DefaultRetentionPeriod = TimeSpan.FromDays(2555), // 7 years
                    PurgeDeletedDataAfter = TimeSpan.FromDays(30)
                },
                ComplianceRequirements = new[]
                {
                    "CCPA Section 1798.105 - Right to delete",
                    "CCPA Section 1798.110 - Right to know",
                    "CCPA Section 1798.115 - Right to opt-out",
                    "CPRA - Sensitive personal information protections"
                }
            },

            // United Kingdom - UK GDPR
            new()
            {
                RegionId = "UK",
                DataCenters = new[] { "eu-west-2" }, // London
                Jurisdiction = new DataJurisdiction
                {
                    LegalFramework = "UK GDPR",
                    TransferRestrictions = new[]
                    {
                        "International transfers require adequacy or appropriate safeguards",
                        "EU transfers subject to adequacy decision status"
                    },
                    RequiresEncryptionAtRest = true,
                    RequiresEncryptionInTransit = true
                },
                RetentionPolicy = new DataRetentionPolicy
                {
                    DefaultRetentionPeriod = TimeSpan.FromDays(1095), // 3 years
                    PurgeDeletedDataAfter = TimeSpan.FromDays(30)
                }
            }
        }
    };
}
```

### Automatic Zone Configuration

During bootstrap, the library automatically configures MongoDB zones based on region data:

```csharp
// Bootstrap with geographic zones
var geographicConfig = GeographicConfiguration.CreateGdprCompliantConfiguration();
var result = await dbCentre.BootstrapEntitiesAsync<CustomerData>(
    new DbBootstrapOptions
    {
        EnableSharding = true,
        EnablePreSplitting = true,
        GeographicConfiguration = geographicConfig
    });

// Results in MongoDB zone configuration:
// sh.addShardToZone("shard01", "EU")
// sh.addShardToZone("shard02", "US")
// sh.addShardToZone("shard03", "UK")
// sh.updateZoneKeyRange("customer_data", { "region": "EU" }, { "region": "EU\uffff" }, "EU")
// sh.updateZoneKeyRange("customer_data", { "region": "US" }, { "region": "US\uffff" }, "US")
// sh.updateZoneKeyRange("customer_data", { "region": "UK" }, { "region": "UK\uffff" }, "UK")
```

## Migration & Compliance

### RegionDataMigrator

The `RegionDataMigrator` class provides comprehensive tools for GDPR-compliant data migration:

```csharp
public class RegionDataMigrator
{
    private readonly IDbManagementProvider _managementProvider;
    private readonly GeographicConfiguration _geographicConfig;

    public RegionDataMigrator(IDbManagementProvider managementProvider, GeographicConfiguration geographicConfig)
    {
        _managementProvider = managementProvider;
        _geographicConfig = geographicConfig;
    }

    // Single entity migration
    public async Task<RegionMigrationResult> MigrateEntityAsync<T>(
        DbObjectId entityId,
        string sourceRegion,
        string targetRegion,
        RegionMigrationOptions? options = null) where T : BaseEntity;

    // Batch migration
    public async Task<RegionBatchMigrationResult> MigrateBatchAsync<T>(
        IEnumerable<DbObjectId> entityIds,
        string sourceRegion,
        string targetRegion,
        RegionMigrationOptions? options = null) where T : BaseEntity;

    // Compliance validation
    public RegionComplianceValidationResult ValidateMigrationCompliance(
        string sourceRegion,
        string targetRegion);
}
```

### Migration Strategies

#### 1. CopyAndDelete (GDPR Right to Portability)

```csharp
// Complete data transfer with source deletion
var result = await migrator.MigrateEntityAsync<Customer>(
    customerId,
    sourceRegion: "US",
    targetRegion: "EU",
    new RegionMigrationOptions
    {
        Strategy = RegionMigrationStrategy.CopyAndDelete,
        VerifyIntegrity = true,
        MaxRetryAttempts = 3
    });

// Process:
// 1. Copy data to target region
// 2. Verify data integrity
// 3. Delete from source region
// 4. Update region field in entity
```

#### 2. CopyAndArchive (Compliance with Retention)

```csharp
// Keep original with retention policy
var result = await migrator.MigrateEntityAsync<Customer>(
    customerId,
    sourceRegion: "US",
    targetRegion: "EU",
    new RegionMigrationOptions
    {
        Strategy = RegionMigrationStrategy.CopyAndArchive,
        VerifyIntegrity = true
    });

// Process:
// 1. Copy data to target region
// 2. Verify data integrity
// 3. Mark source data as archived
// 4. Update region field in entity
// 5. Schedule cleanup after retention period
```

#### 3. PreventMigration (Regulatory Restrictions)

```csharp
// Block transfer for compliance
var options = new RegionMigrationOptions
{
    Strategy = RegionMigrationStrategy.PreventMigration
};

// Results in immediate failure with compliance message
```

#### 4. FederatedAccess (Virtual Access)

```csharp
// Virtual access without data movement
var result = await migrator.MigrateEntityAsync<Customer>(
    customerId,
    sourceRegion: "US",
    targetRegion: "EU",
    new RegionMigrationOptions
    {
        Strategy = RegionMigrationStrategy.FederatedAccess
    });

// Process:
// 1. Create symbolic link/reference in target region
// 2. Update region metadata for federated access
// 3. Configure cross-region access policies
```

### GDPR Right to Data Portability Implementation

```csharp
public async Task<byte[]> ExportCustomerDataForPortabilityAsync(DbObjectId customerId)
{
    // 1. Validate customer consent for data export
    var customer = await Customers.Where(c => c.Id == customerId).FirstOrDefaultAsync();
    if (customer?.ConsentStatus != "granted")
    {
        throw new InvalidOperationException("Customer has not granted consent for data export");
    }

    // 2. Gather all related data across collections
    var customerData = await Customers.Where(c => c.Id == customerId).FirstOrDefaultAsync();
    var orders = await Orders.Where(o => o.CustomerId == customerId).ToListAsync();
    var addresses = await Addresses.Where(a => a.CustomerId == customerId).ToListAsync();
    var preferences = await Preferences.Where(p => p.CustomerId == customerId).ToListAsync();

    // 3. Create GDPR-compliant portable data package
    var exportData = new
    {
        PersonalData = new
        {
            Customer = customerData,
            ContactInformation = addresses,
            Preferences = preferences
        },
        TransactionalData = new
        {
            Orders = orders,
            Payments = await Payments.Where(p => p.CustomerId == customerId).ToListAsync()
        },
        ConsentHistory = await ConsentLogs.Where(c => c.CustomerId == customerId).ToListAsync(),
        ExportMetadata = new
        {
            ExportDate = DateTime.UtcNow,
            ExportRequestId = DbObjectId.NewId(),
            LegalBasis = "GDPR Article 20 - Right to Data Portability",
            DataRetentionNotice = "This export contains personal data valid as of export date",
            ContactForQuestions = "privacy@company.com"
        }
    };

    // 4. Return as structured JSON for portability
    return System.Text.Encoding.UTF8.GetBytes(
        System.Text.Json.JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
}
```

### GDPR Right to be Forgotten Implementation

```csharp
public async Task<bool> EraseCustomerDataAsync(DbObjectId customerId, string legalBasis)
{
    try
    {
        // 1. Validate legal basis for erasure
        if (!IsValidErasureBasis(legalBasis))
        {
            throw new InvalidOperationException($"Invalid legal basis for erasure: {legalBasis}");
        }

        // 2. Identify all data related to customer across collections
        var collectionsToErase = new[]
        {
            (Name: "customers", DeleteAction: () => Customers.Where(c => c.Id == customerId).DeleteAsync()),
            (Name: "orders", DeleteAction: () => Orders.Where(o => o.CustomerId == customerId).DeleteAsync()),
            (Name: "addresses", DeleteAction: () => Addresses.Where(a => a.CustomerId == customerId).DeleteAsync()),
            (Name: "preferences", DeleteAction: () => Preferences.Where(p => p.CustomerId == customerId).DeleteAsync()),
            (Name: "marketing_consents", DeleteAction: () => MarketingConsents.Where(m => m.CustomerId == customerId).DeleteAsync())
        };

        // 3. Perform secure deletion across all collections
        var deletedCollections = new List<string>();
        foreach (var (name, deleteAction) in collectionsToErase)
        {
            try
            {
                await deleteAction();
                deletedCollections.Add(name);
            }
            catch (Exception ex)
            {
                // Log partial failure but continue with other collections
                Console.WriteLine($"Failed to delete from {name}: {ex.Message}");
            }
        }

        // 4. Log erasure for compliance audit trail
        await ComplianceAudit.InsertAsync(new ComplianceAuditEntry
        {
            CustomerId = customerId,
            AuditDate = DateTime.UtcNow,
            ActionType = "erasure",
            LegalBasis = legalBasis,
            ComplianceFramework = "GDPR Article 17",
            RequestedBy = "system", // or actual user ID
            AuditDetails = new Dictionary<string, object>
            {
                ["deleted_collections"] = deletedCollections,
                ["erasure_method"] = "secure_deletion",
                ["verification_status"] = "completed"
            }
        });

        return true;
    }
    catch (Exception ex)
    {
        // Log failure for compliance audit trail
        await ComplianceAudit.InsertAsync(new ComplianceAuditEntry
        {
            CustomerId = customerId,
            AuditDate = DateTime.UtcNow,
            ActionType = "erasure_failure",
            LegalBasis = legalBasis,
            ComplianceFramework = "GDPR Article 17",
            AuditDetails = new Dictionary<string, object>
            {
                ["error_message"] = ex.Message,
                ["failure_reason"] = "technical_error"
            }
        });

        throw;
    }
}

private bool IsValidErasureBasis(string legalBasis)
{
    var validBases = new[]
    {
        "GDPR Article 17(1)(a) - Consent withdrawn",
        "GDPR Article 17(1)(b) - No longer necessary",
        "GDPR Article 17(1)(c) - Unlawful processing",
        "GDPR Article 17(1)(d) - Objection to processing",
        "GDPR Article 17(1)(e) - Legal obligation",
        "GDPR Article 17(1)(f) - Child data protection"
    };

    return validBases.Contains(legalBasis);
}
```

## Best Practices

### 1. Region Assignment Strategy

```csharp
public static class RegionAssignmentHelper
{
    public static string DetermineRegionFromUserContext(string userLocation, string userPreference = null)
    {
        // Priority 1: User explicit preference (if legally allowed)
        if (!string.IsNullOrEmpty(userPreference) && IsRegionAllowed(userPreference, userLocation))
        {
            return userPreference;
        }

        // Priority 2: Legal requirement based on location
        return userLocation switch
        {
            var loc when IsEuCountry(loc) => "EU",
            var loc when IsUkTerritory(loc) => "UK",
            var loc when IsUsTerritory(loc) => "US",
            var loc when IsCanadianTerritory(loc) => "CA",
            var loc when IsChineseTerritory(loc) => "CN",
            _ => "US" // Default fallback
        };
    }

    private static bool IsRegionAllowed(string requestedRegion, string userLocation)
    {
        // Example: EU users cannot choose non-EU regions without explicit consent
        if (IsEuCountry(userLocation) && requestedRegion != "EU")
        {
            return false; // Requires additional consent flow
        }
        return true;
    }
}
```

### 2. Query Optimization for Geographic Distribution

```csharp
public class CustomerRepository : DataRepository
{
    public MongoQuery<Customer> Customers => new(_adapter.GetCollection<Customer>());

    // GOOD: Region-specific queries (hits only relevant shards)
    public async Task<List<Customer>> GetEuCustomersAsync()
    {
        return await Customers
            .Where(c => c.Region == "EU")
            .Where(c => c.IsActive == true)
            .ToListAsync();
    }

    // CAREFUL: Cross-region queries (may hit multiple zones)
    public async Task<List<Customer>> GetAllActiveCustomersAsync()
    {
        return await Customers
            .Where(c => c.IsActive == true)
            .ToListAsync(); // May hit multiple geographic zones
    }

    // OPTIMAL: Region-aware pagination
    public async Task<CustomerCollection> GetCustomersPagedAsync(string region, int pageIndex, int pageSize)
    {
        return await Customers
            .Where(c => c.Region == region)
            .Where(c => c.IsActive == true)
            .OrderBy(c => c.CreatedOnUtc)
            .FetchAsync<Customer, CustomerCollection>(pageIndex, pageSize);
    }
}
```

### 3. Compliance Monitoring

```csharp
public class ComplianceMonitoringService
{
    public async Task<ComplianceReport> GenerateComplianceReportAsync(string region, DateTime fromDate, DateTime toDate)
    {
        var auditEntries = await ComplianceAudit
            .Where(a => a.Region == region)
            .Where(a => a.AuditDate >= fromDate && a.AuditDate <= toDate)
            .ToListAsync();

        return new ComplianceReport
        {
            Region = region,
            ReportPeriod = new { From = fromDate, To = toDate },
            DataSubjectRequests = auditEntries.Count(a => a.ActionType.Contains("request")),
            ErasureRequests = auditEntries.Count(a => a.ActionType == "erasure"),
            PortabilityRequests = auditEntries.Count(a => a.ActionType == "export"),
            CrossRegionMigrations = auditEntries.Count(a => a.ActionType == "migration"),
            ComplianceViolations = auditEntries.Count(a => a.ActionType.Contains("violation"))
        };
    }
}
```

### 4. Data Classification and Handling

```csharp
public enum DataClassification
{
    Public,        // No restrictions
    Internal,      // Company confidential
    Confidential,  // Customer data
    Restricted,    // PII/PHI requiring special handling
    Regulated      // Data subject to specific regulations
}

[DbCollection("classified_data", EnableSharding = true)]
[DbShardKey("DataClassification", "OwnerId", IncludeRegion = true, RegionStrategy = RegionShardingStrategy.RegionFirst)]
[DbIndex("idx_classification_audit", "DataClassification", "Region", "LastAuditDate")]
public class ClassifiedData : BaseEntity
{
    [DbField("data_classification")]
    public DataClassification DataClassification { get; set; }

    [DbField("owner_id")]
    public DbObjectId OwnerId { get; set; }

    [DbField("content")]
    public string Content { get; set; } = string.Empty;

    [DbField("encryption_status")]
    public string EncryptionStatus { get; set; } = "encrypted_at_rest";

    [DbField("last_audit_date")]
    public DateTime LastAuditDate { get; set; }

    [DbField("retention_category")]
    public string RetentionCategory { get; set; } = string.Empty;
}
```

## Examples

### Complete GDPR Implementation Example

```csharp
public class GdprCompliantApplication
{
    private readonly MongoDbCentre _dbCentre;
    private readonly RegionDataMigrator _migrator;
    private readonly GeographicConfiguration _geographicConfig;

    public GdprCompliantApplication(string connectionString)
    {
        _dbCentre = new MongoDbCentre(connectionString);
        _geographicConfig = GeographicConfiguration.CreateGdprCompliantConfiguration();
        _migrator = new RegionDataMigrator(_dbCentre, _geographicConfig);
    }

    public async Task<bool> InitializeAsync()
    {
        // Bootstrap with GDPR-compliant configuration
        var result = await _dbCentre.BootstrapEntitiesAsync<GdprCustomer, ComplianceAuditEntry>(
            new DbBootstrapOptions
            {
                EnableSharding = true,
                EnablePreSplitting = true,
                GeographicConfiguration = _geographicConfig,
                CreateIndexesInBackground = true
            });

        return result.Success;
    }

    public async Task<bool> HandleDataSubjectRequestAsync(DataSubjectRequest request)
    {
        switch (request.RequestType)
        {
            case "access":
                return await HandleAccessRequestAsync(request.CustomerId);
            case "portability":
                return await HandlePortabilityRequestAsync(request.CustomerId);
            case "erasure":
                return await HandleErasureRequestAsync(request.CustomerId, request.LegalBasis);
            case "rectification":
                return await HandleRectificationRequestAsync(request.CustomerId, request.UpdateData);
            default:
                throw new NotSupportedException($"Request type '{request.RequestType}' not supported");
        }
    }

    private async Task<bool> HandleAccessRequestAsync(DbObjectId customerId)
    {
        var customer = await _dbCentre.GetMongoDbCollection<GdprCustomer>()
            .Find(c => c.Id == customerId)
            .FirstOrDefaultAsync();

        if (customer == null) return false;

        // Generate comprehensive data access report
        var accessReport = new
        {
            PersonalData = customer,
            ProcessingHistory = await GetProcessingHistoryAsync(customerId),
            ConsentStatus = customer.ConsentStatus,
            DataRetentionInfo = await GetRetentionInfoAsync(customerId),
            ThirdPartySharing = await GetThirdPartySharingAsync(customerId)
        };

        // Log access request for audit trail
        await LogComplianceActionAsync(customerId, "access", "GDPR Article 15", accessReport);

        return true;
    }
}
```

This comprehensive documentation provides all the necessary information for implementing region-aware sharding and GDPR compliance using the OElite.Restme.MongoDb library. The examples demonstrate real-world usage patterns and best practices for maintaining regulatory compliance while achieving optimal performance.