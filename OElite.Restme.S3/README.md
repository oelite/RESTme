# OElite.Restme.S3

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.S3.svg)](https://www.nuget.org/packages/OElite.Restme.S3)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Amazon S3 and S3-compatible storage integration package for the Restme framework, providing scalable cloud storage capabilities with enterprise-grade features.

## Overview

OElite.Restme.S3 provides comprehensive Amazon S3 and S3-compatible storage integration for the OElite platform. Built on the AWS SDK for .NET, it offers high-performance file storage, object management, and distributed storage capabilities with support for multiple S3 providers including AWS S3, MinIO, and other S3-compatible services.

## Features

- **S3-Compatible Storage**: Full support for AWS S3 and S3-compatible providers (MinIO, DigitalOcean Spaces, etc.)
- **Enterprise Performance**: Optimized for high-throughput scenarios and large file operations
- **Flexible Configuration**: Support for multiple S3 endpoints, regions, and authentication methods
- **Type-Safe Operations**: Strongly-typed storage operations with automatic serialization
- **Stream Support**: Direct stream operations for efficient memory usage
- **Path-Style Support**: Configurable path-style vs virtual-hosted-style requests
- **Regional Support**: Multi-region storage with geographic data placement
- **Error Handling**: Comprehensive error handling with custom exceptions
- **Async/Await Support**: Full asynchronous operations for optimal performance

## Installation

```bash
dotnet add package OElite.Restme.S3
```

## Quick Start

### Basic Configuration

```csharp
using OElite.Providers;

// AWS S3 configuration
var connectionString = "region=us-west-2;bucket=my-bucket;accesskey=YOUR_ACCESS_KEY;secretkey=YOUR_SECRET_KEY";
var config = new RestConfig();
var storageProvider = new S3StorageProvider(connectionString, config);

// MinIO configuration
var minioConnection = "endpoint=https://minio.example.com:9000;bucket=my-bucket;accesskey=minioadmin;secretkey=minioadmin;forcepath=true";
var minioProvider = new S3StorageProvider(minioConnection, config);
```

### Basic Storage Operations

```csharp
// Store data in S3
await storageProvider.SetAsync("documents/report.json", reportData);

// Retrieve data from S3
var report = await storageProvider.GetAsync<ReportData>("documents/report.json");

// Check if object exists
bool exists = await storageProvider.ExistsAsync("documents/report.json");

// Remove object
await storageProvider.RemoveAsync("documents/report.json");
```

## Core Features

### Object Storage

Store and retrieve various data types:

```csharp
// Store complex objects
public class Document
{
    public string Id { get; set; }
    public string Title { get; set; }
    public byte[] Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

var document = new Document
{
    Id = "doc-123",
    Title = "Important Document",
    Content = File.ReadAllBytes("document.pdf"),
    CreatedAt = DateTime.UtcNow
};

// Store in S3 with organized key structure
await storageProvider.SetAsync($"documents/{document.Id}/metadata.json", document);
```

### Stream Operations

Efficient handling of large files:

```csharp
// Upload large file using stream
using var fileStream = File.OpenRead("large-file.zip");
await storageProvider.SetStreamAsync("uploads/large-file.zip", fileStream);

// Download file as stream
using var downloadStream = await storageProvider.GetStreamAsync("uploads/large-file.zip");
using var outputFile = File.Create("downloaded-file.zip");
await downloadStream.CopyToAsync(outputFile);
```

### Hierarchical Storage

Organize objects with path-like keys:

```csharp
// Organize files in logical hierarchy
await storageProvider.SetAsync("users/123/profile/avatar.jpg", avatarData);
await storageProvider.SetAsync("users/123/documents/resume.pdf", resumeData);
await storageProvider.SetAsync("products/456/images/main.jpg", imageData);
await storageProvider.SetAsync("products/456/specs/datasheet.pdf", specData);

// List objects with prefix
var userFiles = await storageProvider.ListObjectsAsync("users/123/");
```

### Batch Operations

Efficient bulk operations:

```csharp
// Store multiple files efficiently
var uploadTasks = new List<Task>();
foreach (var file in files)
{
    var key = $"batch-upload/{file.Name}";
    uploadTasks.Add(storageProvider.SetAsync(key, file.Data));
}
await Task.WhenAll(uploadTasks);

// Retrieve multiple objects
var downloadTasks = fileKeys.Select(key => storageProvider.GetAsync<FileData>(key));
var results = await Task.WhenAll(downloadTasks);
```

## Configuration Options

### Connection String Format

```csharp
// AWS S3 (region-based)
"region=us-east-1;bucket=my-bucket;accesskey=ACCESS_KEY;secretkey=SECRET_KEY"

// AWS S3 with custom endpoint
"endpoint=https://s3.amazonaws.com;region=eu-west-1;bucket=my-bucket;accesskey=ACCESS_KEY;secretkey=SECRET_KEY"

// MinIO (self-hosted)
"endpoint=https://minio.example.com:9000;bucket=storage;accesskey=minioadmin;secretkey=minioadmin;forcepath=true;usehttp=true"

// DigitalOcean Spaces
"endpoint=https://fra1.digitaloceanspaces.com;region=fra1;bucket=my-space;accesskey=ACCESS_KEY;secretkey=SECRET_KEY"

// Custom S3-compatible service
"endpoint=https://storage.example.com;bucket=data;accesskey=user;secretkey=password;forcepath=true"
```

### S3Configuration Properties

```csharp
public class S3Configuration
{
    public string ServiceUrl { get; set; }      // S3 endpoint URL
    public RegionEndpoint Region { get; set; }  // AWS region
    public string BucketName { get; set; }      // S3 bucket name
    public string AccessKeyId { get; set; }     // Access key
    public string SecretAccessKey { get; set; }  // Secret key
    public bool ForcePathStyle { get; set; }    // Use path-style URLs
    public bool UseHttp { get; set; }           // Use HTTP instead of HTTPS
}
```

## Advanced Usage

### Regional Data Placement

Store data in specific regions for compliance:

```csharp
// EU data in EU region
var euConnection = "region=eu-west-1;bucket=eu-data;accesskey=KEY;secretkey=SECRET";
var euProvider = new S3StorageProvider(euConnection, config);

await euProvider.SetAsync("gdpr/user-123/data.json", userData);

// US data in US region
var usConnection = "region=us-east-1;bucket=us-data;accesskey=KEY;secretkey=SECRET";
var usProvider = new S3StorageProvider(usConnection, config);

await usProvider.SetAsync("users/user-456/profile.json", profileData);
```

### Content Metadata

Store additional metadata with objects:

```csharp
// Store with content type and custom metadata
var metadata = new Dictionary<string, string>
{
    ["ContentType"] = "application/pdf",
    ["OriginalName"] = "document.pdf",
    ["UploadedBy"] = "user-123",
    ["Department"] = "HR"
};

await storageProvider.SetWithMetadataAsync("documents/file.pdf", pdfData, metadata);

// Retrieve with metadata
var (data, meta) = await storageProvider.GetWithMetadataAsync<byte[]>("documents/file.pdf");
```

### Error Handling

```csharp
try
{
    var data = await storageProvider.GetAsync<Document>("missing-file.json");
}
catch (OEliteException ex) when (ex.Message.Contains("not found"))
{
    // Handle missing objects
    _logger.LogWarning("Object not found: {Key}", key);
    return null;
}
catch (OEliteException ex) when (ex.Message.Contains("access denied"))
{
    // Handle permission issues
    _logger.LogError("S3 access denied: {Error}", ex.Message);
    throw;
}
catch (Exception ex)
{
    // Handle other S3 errors
    _logger.LogError(ex, "S3 operation failed");
    throw;
}
```

## Integration Patterns

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IStorageProvider>(provider =>
{
    var connectionString = configuration.GetConnectionString("S3Storage");
    var config = new RestConfig();
    return new S3StorageProvider(connectionString, config);
});

// In your service
public class FileService
{
    private readonly IStorageProvider _storage;

    public FileService(IStorageProvider storage)
    {
        _storage = storage;
    }

    public async Task<string> SaveFileAsync(string fileName, byte[] content)
    {
        var key = $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{fileName}";
        await _storage.SetAsync(key, content);
        return key;
    }

    public async Task<byte[]> GetFileAsync(string key)
    {
        return await _storage.GetAsync<byte[]>(key);
    }
}
```

### Document Storage

```csharp
public class DocumentManager
{
    private readonly IStorageProvider _storage;

    public DocumentManager(IStorageProvider storage)
    {
        _storage = storage;
    }

    public async Task<string> StoreDocumentAsync(string documentId, Document document)
    {
        var metadataKey = $"documents/{documentId}/metadata.json";
        var contentKey = $"documents/{documentId}/content.{document.Extension}";

        // Store metadata
        await _storage.SetAsync(metadataKey, new DocumentMetadata
        {
            Id = document.Id,
            Name = document.Name,
            ContentType = document.ContentType,
            Size = document.Content.Length,
            UploadedAt = DateTime.UtcNow
        });

        // Store content
        await _storage.SetAsync(contentKey, document.Content);

        return documentId;
    }

    public async Task<Document> GetDocumentAsync(string documentId)
    {
        var metadataKey = $"documents/{documentId}/metadata.json";
        var metadata = await _storage.GetAsync<DocumentMetadata>(metadataKey);

        if (metadata == null) return null;

        var contentKey = $"documents/{documentId}/content.{metadata.Extension}";
        var content = await _storage.GetAsync<byte[]>(contentKey);

        return new Document
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Content = content,
            ContentType = metadata.ContentType
        };
    }
}
```

### Cache Integration

Combine with Redis for multi-tier storage:

```csharp
public class TieredStorageService
{
    private readonly ICacheProvider _cache;
    private readonly IStorageProvider _storage;

    public TieredStorageService(ICacheProvider cache, IStorageProvider storage)
    {
        _cache = cache;
        _storage = storage;
    }

    public async Task<T> GetDataAsync<T>(string key) where T : class
    {
        // Try cache first (fast)
        var cached = await _cache.GetAsync<T>(key);
        if (cached != null) return cached;

        // Fallback to S3 (slower but persistent)
        var stored = await _storage.GetAsync<T>(key);
        if (stored != null)
        {
            // Cache for future requests
            await _cache.SetAsync(key, stored, TimeSpan.FromMinutes(15));
        }

        return stored;
    }

    public async Task SetDataAsync<T>(string key, T data, TimeSpan? cacheExpiry = null) where T : class
    {
        // Store in both cache and S3
        var tasks = new[]
        {
            _cache.SetAsync(key, data, cacheExpiry ?? TimeSpan.FromMinutes(15)),
            _storage.SetAsync(key, data)
        };

        await Task.WhenAll(tasks);
    }
}
```

## Performance Considerations

### Connection Optimization

```csharp
// Reuse S3 client instances
var provider = new S3StorageProvider(connectionString, config);

// All operations use the same client
await provider.SetAsync("file1", data1);
await provider.SetAsync("file2", data2);
await provider.GetAsync<Data>("file1");
```

### Large File Handling

```csharp
// Use streams for large files to minimize memory usage
public async Task UploadLargeFileAsync(string filePath, string s3Key)
{
    using var fileStream = File.OpenRead(filePath);
    await _storage.SetStreamAsync(s3Key, fileStream);
}

// Process large downloads in chunks
public async Task DownloadLargeFileAsync(string s3Key, string outputPath)
{
    using var s3Stream = await _storage.GetStreamAsync(s3Key);
    using var outputStream = File.Create(outputPath);

    var buffer = new byte[8192];
    int bytesRead;
    while ((bytesRead = await s3Stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        await outputStream.WriteAsync(buffer, 0, bytesRead);
    }
}
```

### Key Naming Best Practices

```csharp
// Good: Hierarchical and predictable
$"users/{userId}/documents/{documentId}.pdf"
$"products/{productId}/images/{imageType}/{timestamp}.jpg"
$"backups/{date:yyyy/MM/dd}/database-backup.zip"

// Avoid: Flat structure without organization
$"user_doc_{userId}_{documentId}.pdf"
$"random_file_12345.dat"
```

## Provider Compatibility

### AWS S3
- Full feature support
- All regions supported
- IAM integration

### MinIO
- Complete S3 API compatibility
- Self-hosted deployments
- Development and testing

### DigitalOcean Spaces
- S3-compatible API
- Geographic regions
- CDN integration

### Other S3-Compatible Services
- Wasabi
- Backblaze B2
- IBM Cloud Object Storage

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **AWSSDK.S3 3.7.401+**
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

S3StorageProvider is thread-safe and designed for concurrent operations:

- AWS SDK client is thread-safe
- Can be used as a singleton in DI containers
- Supports parallel uploads and downloads

## License

Copyright © OElite Limited. All rights reserved.