# OElite.Restme.GoogleUtils

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.GoogleUtils.svg)](https://www.nuget.org/packages/OElite.Restme.GoogleUtils)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Google Cloud Services integration package for the Restme framework, providing Google Cloud Storage, Firestore, and other Google Cloud Platform capabilities.

## Overview

OElite.Restme.GoogleUtils provides comprehensive Google Cloud Platform integration for the OElite platform. Built on the Google Cloud Client Libraries for .NET, it offers high-performance cloud storage, NoSQL database operations, and enterprise-grade cloud services with native Google Cloud integration.

## Features

- **Generic Provider Factory**: Auto-registered via `ServiceLocator` with multiple capability support
- **Provider Capabilities**: `ProviderCapabilities.Cache | Storage | Search` - Google Cloud provides multiple services
- **Google Cloud Storage**: Full support for GCS buckets, objects, and access control
- **Google Firestore**: NoSQL document database operations for real-time applications
- **Google Cloud Search**: Integration with Google Cloud Search API for content discovery
- **Enterprise Performance**: Optimized for high-throughput scenarios and global distribution
- **Flexible Authentication**: Support for service accounts, ADC, and API keys
- **Type-Safe Operations**: Strongly-typed operations with automatic serialization
- **Stream Support**: Direct stream operations for efficient memory usage
- **Global Distribution**: Multi-region support with automatic geographic optimization
- **Error Handling**: Comprehensive error handling with Google Cloud-specific exceptions
- **Async/Await Support**: Full asynchronous operations for optimal performance

## Installation

```bash
dotnet add package OElite.Restme.GoogleUtils
```

## Quick Start

### Basic Configuration

```csharp
using OElite;
using OElite.Abstractions;

// Option 1: Using Rest with generic provider pattern (recommended)
var rest = new Rest("https://storage.googleapis.com",
    configuration: new RestConfig
    {
        OperationMode = RestMode.GoogleCloud,
        AuthKey = "service-account-email",
        AuthSecret = "path-to-service-account.json",
        InstanceName = "my-bucket",
        Region = "us-central1"
    });

// Get providers using generic factory pattern
var storageProvider = rest.GetProvider<IStorageProvider>();
var cacheProvider = rest.GetProvider<ICacheProvider>();
var searchProvider = rest.GetProvider<ISearchProvider>();

// NEW: Named providers for multiple GCP projects/services
var documentsProvider = rest.GetProvider<IStorageProvider>("documents");
var firestoreProvider = rest.GetProvider<ICacheProvider>("firestore");
var searchProvider = rest.GetProvider<ISearchProvider>("cloud-search");

// Option 2: Direct provider instantiation (still supported)
var config = new RestConfig
{
    AuthKey = "service-account-email",
    AuthSecret = "path-to-service-account.json",
    InstanceName = "my-bucket"
};
var directProvider = new GoogleCloudStorageProvider("https://storage.googleapis.com", config);
```

### Basic Storage Operations

```csharp
// Store data in Google Cloud Storage
await storageProvider.SetAsync("documents/report.json", reportData);

// Retrieve data from Google Cloud Storage
var report = await storageProvider.GetAsync<ReportData>("documents/report.json");

// Check if object exists
bool exists = await storageProvider.ExistsAsync("documents/report.json");

// Remove object
await storageProvider.RemoveAsync("documents/report.json");
```

## Configuration Options

### Authentication Configuration

#### Using RestConfig (Recommended)
```csharp
var config = new RestConfig
{
    AuthKey = "service-account-email@project.iam.gserviceaccount.com",
    AuthSecret = "/path/to/service-account.json",      // Path to service account key file
    InstanceName = "bucket-name",                      // GCS bucket name
    Region = "us-central1",                           // GCS region
    OperationMode = RestMode.GoogleCloud
};
var storageProvider = new GoogleCloudStorageProvider("https://storage.googleapis.com", config);
```

#### Google Cloud Connection Formats
```csharp
// Service Account with JSON key file
"project_id=my-project;bucket=my-bucket;service_account_path=/path/to/key.json"

// Application Default Credentials (for GKE, Cloud Run, etc.)
"project_id=my-project;bucket=my-bucket;use_default_credentials=true"

// API Key authentication (limited scope)
"project_id=my-project;bucket=my-bucket;api_key=your-api-key"

// Development emulator
"project_id=test-project;bucket=test-bucket;emulator_host=localhost:8080"
```

## Core Features

### Google Cloud Storage

High-performance object storage:

```csharp
// Store objects with metadata
var metadata = new Dictionary<string, string>
{
    ["ContentType"] = "application/json",
    ["UploadedBy"] = "user123",
    ["Project"] = "analytics"
};

await storageProvider.SetWithMetadataAsync("data/analytics.json", analyticsData, metadata);

// Stream operations for large files
using var fileStream = File.OpenRead("large-dataset.csv");
await storageProvider.SetStreamAsync("datasets/large-dataset.csv", fileStream);

// Download as stream
using var downloadStream = await storageProvider.GetStreamAsync("datasets/large-dataset.csv");
```

### Google Firestore Integration

NoSQL document database operations:

```csharp
var cacheProvider = rest.GetProvider<ICacheProvider>("firestore");

// Store document with automatic ID
await cacheProvider.SetAsync("users/user123", userData);

// Query documents
var users = await cacheProvider.QueryAsync<User>("users",
    filter: u => u.Status == "active" && u.LastLogin > DateTime.UtcNow.AddDays(-30));

// Real-time listeners
await cacheProvider.ListenAsync<User>("users/user123", user =>
{
    Console.WriteLine($"User updated: {user.Name}");
});
```

### Google Cloud Search

Content discovery and search capabilities:

```csharp
var searchProvider = rest.GetProvider<ISearchProvider>();

// Index content
await searchProvider.IndexAsync("documents", new SearchDocument
{
    Id = "doc-123",
    Title = "Important Document",
    Content = "Document content for search indexing",
    Tags = new[] { "important", "legal", "contract" }
});

// Search content
var results = await searchProvider.SearchAsync("legal contract", new SearchOptions
{
    MaxResults = 50,
    IncludeSnippets = true
});

foreach (var result in results.Documents)
{
    Console.WriteLine($"Found: {result.Title} (Score: {result.Score})");
}
```

## Integration Patterns

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IStorageProvider>(provider =>
{
    var config = new RestConfig
    {
        AuthSecret = configuration["GoogleCloud:ServiceAccountPath"],
        InstanceName = configuration["GoogleCloud:BucketName"]
    };
    return new GoogleCloudStorageProvider("https://storage.googleapis.com", config);
});

services.AddSingleton<ICacheProvider>(provider =>
{
    var config = new RestConfig
    {
        AuthSecret = configuration["GoogleCloud:ServiceAccountPath"],
        InstanceName = configuration["GoogleCloud:ProjectId"]
    };
    return new FirestoreProvider("https://firestore.googleapis.com", config);
});
```

### Multi-Service Integration

```csharp
public class GoogleCloudService
{
    private readonly IStorageProvider _storage;
    private readonly ICacheProvider _firestore;
    private readonly ISearchProvider _search;

    public GoogleCloudService(Rest rest)
    {
        _storage = rest.GetProvider<IStorageProvider>("gcs");
        _firestore = rest.GetProvider<ICacheProvider>("firestore");
        _search = rest.GetProvider<ISearchProvider>("search");
    }

    public async Task<string> ProcessDocumentAsync(string fileName, byte[] content)
    {
        // Store in GCS
        var storageKey = $"documents/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}/{fileName}";
        await _storage.SetAsync(storageKey, content);

        // Index metadata in Firestore
        var metadata = new DocumentMetadata
        {
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            StorageKey = storageKey,
            UploadedAt = DateTime.UtcNow,
            Size = content.Length
        };
        await _firestore.SetAsync($"documents/{metadata.Id}", metadata);

        // Index for search
        await _search.IndexAsync("documents", new SearchDocument
        {
            Id = metadata.Id,
            Title = fileName,
            Content = ExtractTextContent(content),
            Metadata = new { storageKey, uploadedAt = metadata.UploadedAt }
        });

        return metadata.Id;
    }

    public async Task<(byte[] content, DocumentMetadata metadata)> GetDocumentAsync(string documentId)
    {
        // Get metadata from Firestore
        var metadata = await _firestore.GetAsync<DocumentMetadata>($"documents/{documentId}");
        if (metadata == null) return (null, null);

        // Get content from GCS
        var content = await _storage.GetAsync<byte[]>(metadata.StorageKey);

        return (content, metadata);
    }
}
```

## Advanced Features

### Regional Data Placement

```csharp
// Store data in specific regions for compliance
var euConfig = new RestConfig
{
    AuthSecret = "service-account.json",
    InstanceName = "eu-data-bucket",
    Region = "europe-west1"
};
var euProvider = new GoogleCloudStorageProvider("https://storage.googleapis.com", euConfig);

// GDPR-compliant storage
await euProvider.SetAsync("gdpr/user-123/data.json", userData);
```

### IAM and Security

```csharp
// Configure bucket-level permissions
var config = new RestConfig
{
    AuthSecret = "service-account.json",
    InstanceName = "secure-bucket",
    // Additional security settings
    SecuritySettings = new GoogleCloudSecuritySettings
    {
        UniformBucketAccess = true,
        PublicAccessPrevention = true,
        RequireSignedUrls = true
    }
};
```

### Performance Optimization

```csharp
// Parallel uploads for better performance
public async Task BulkUploadAsync(Dictionary<string, byte[]> files)
{
    var uploadTasks = files.Select(kvp =>
        _storage.SetAsync(kvp.Key, kvp.Value)).ToList();

    // Upload in batches to avoid overwhelming the service
    const int batchSize = 10;
    for (int i = 0; i < uploadTasks.Count; i += batchSize)
    {
        var batch = uploadTasks.Skip(i).Take(batchSize);
        await Task.WhenAll(batch);
    }
}
```

## Error Handling

```csharp
try
{
    var data = await storageProvider.GetAsync<Document>("path/to/document.json");
}
catch (OEliteException ex) when (ex.Message.Contains("NotFound"))
{
    _logger.LogWarning("Document not found in GCS: {Path}", path);
    return null;
}
catch (OEliteException ex) when (ex.Message.Contains("Forbidden"))
{
    _logger.LogError("Access denied to GCS resource: {Error}", ex.Message);
    throw;
}
catch (OEliteException ex) when (ex.Message.Contains("QuotaExceeded"))
{
    _logger.LogError("GCS quota exceeded: {Error}", ex.Message);
    // Implement retry with backoff
    throw;
}
```

## Integration with OElite Platform

### Configuration in OElite Applications

```csharp
// In appsettings.json
{
  "oelite": {
    "storage": {
      "google": {
        "app": "project_id=my-project;bucket=my-bucket;service_account_path=/path/to/key.json"
      }
    }
  }
}

// In your service
public class DocumentService
{
    private readonly IStorageProvider _gcsStorage;

    public DocumentService(IAppConfig appConfig)
    {
        _gcsStorage = appConfig.GoogleStorage;
    }
}
```

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **Google.Cloud.Storage.V1 4.6.0+**
- **Google.Cloud.Firestore 3.4.0+** (for Firestore features)
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

GoogleCloudProvider implementations are thread-safe:

- Google Cloud client libraries are thread-safe
- Can be used as singletons in DI containers
- Support concurrent operations across services

## Best Practices

### Authentication
- Use service accounts for production
- Store service account keys securely
- Use Application Default Credentials in GCP environments
- Implement least-privilege access policies

### Storage Organization
- Use hierarchical object naming
- Implement lifecycle policies for cost optimization
- Use regional storage for data locality
- Enable versioning for critical data

### Performance
- Use parallel operations for bulk tasks
- Implement exponential backoff for retries
- Cache frequently accessed data
- Use streaming for large objects

## License

Copyright © Phanes Technology Ltd. All rights reserved.