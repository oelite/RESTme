# OElite.Restme.Azure

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.Azure.svg)](https://www.nuget.org/packages/OElite.Restme.Azure)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Azure Cloud Storage integration package for the Restme framework, providing Azure Blob Storage and Table Storage capabilities with enterprise-grade features.

## Overview

OElite.Restme.Azure provides comprehensive Azure Cloud Storage integration for the OElite platform. Built on the Azure SDK for .NET, it offers high-performance blob storage, table operations, and distributed storage capabilities with native Azure integration features.

## Features

- **Generic Provider Factory**: Auto-registered via `ServiceLocator` with dual capability support
- **Provider Capabilities**: `ProviderCapabilities.Cache | Storage` - Azure provides both caching and storage
- **Azure Blob Storage**: Full support for Azure Blob Storage with block, page, and append blobs
- **Azure Table Storage**: NoSQL table operations for structured data
- **Enterprise Performance**: Optimized for high-throughput scenarios and large file operations
- **Flexible Configuration**: Support for connection strings, SAS tokens, and managed identity
- **Type-Safe Operations**: Strongly-typed storage operations with automatic serialization
- **Stream Support**: Direct stream operations for efficient memory usage
- **Access Control**: Support for Azure RBAC, SAS tokens, and container-level permissions
- **Regional Support**: Multi-region storage with geographic data placement
- **Error Handling**: Comprehensive error handling with Azure-specific exceptions
- **Async/Await Support**: Full asynchronous operations for optimal performance

## Installation

```bash
dotnet add package OElite.Restme.Azure
```

## Quick Start

### Basic Configuration

```csharp
using OElite;
using OElite.Abstractions;

// Option 1: Using Rest with generic provider pattern (recommended)
var rest = new Rest("https://myaccount.blob.core.windows.net",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Azure,
        AuthKey = "myaccount",
        AuthSecret = "account-key",
        InstanceName = "my-container"
    });

// Get providers using generic factory pattern
var storageProvider = rest.GetProvider<IStorageProvider>();
var cacheProvider = rest.GetProvider<ICacheProvider>(); // Azure also supports caching!

// NEW: Named providers for multiple containers/purposes
var documentsProvider = rest.GetProvider<IStorageProvider>("documents");
var imagesProvider = rest.GetProvider<IStorageProvider>("images");
var backupsProvider = rest.GetProvider<IStorageProvider>("backups");
var cachingProvider = rest.GetProvider<ICacheProvider>("cache");

// Option 2: Direct provider instantiation (still supported)
var config = new RestConfig
{
    AuthKey = "myaccount",
    AuthSecret = "account-key",
    InstanceName = "my-container"
};
var directProvider = new AzureStorageProvider("https://myaccount.blob.core.windows.net", config);
```

### Basic Storage Operations

```csharp
// Store data in Azure Blob Storage
await storageProvider.SetAsync("documents/report.json", reportData);

// Retrieve data from Azure Blob Storage
var report = await storageProvider.GetAsync<ReportData>("documents/report.json");

// Check if blob exists
bool exists = await storageProvider.ExistsAsync("documents/report.json");

// Remove blob
await storageProvider.RemoveAsync("documents/report.json");
```

## Configuration Options

### Authentication Configuration

#### Using RestConfig (Recommended)
```csharp
var config = new RestConfig
{
    AuthKey = "storage_account_name",        // Azure Storage Account name
    AuthSecret = "storage_account_key",      // Azure Storage Account key
    InstanceName = "container_name",         // Azure Blob container name
    OperationMode = RestMode.Azure
};
var storageProvider = new AzureStorageProvider("https://account.blob.core.windows.net", config);
```

#### Azure Connection String Format
```csharp
// Standard Azure Storage connection string
"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"

// With custom endpoint
"DefaultEndpointsProtocol=https;BlobEndpoint=https://myaccount.blob.core.windows.net;AccountName=myaccount;AccountKey=mykey"

// Using SAS token
"BlobEndpoint=https://myaccount.blob.core.windows.net;SharedAccessSignature=sv=2020-04-08&ss=b&srt=c&sp=rwlac&se=2024-12-31T23:59:59Z&st=2024-01-01T00:00:00Z&spr=https&sig=signature"

// Azure Storage Emulator (for development)
"UseDevelopmentStorage=true"
```

## Core Features

### Blob Storage Operations

Store and retrieve various blob types:

```csharp
// Block blobs (default - for general files)
await storageProvider.SetAsync("documents/file.pdf", pdfData);

// Stream operations for large files
using var fileStream = File.OpenRead("large-file.zip");
await storageProvider.SetStreamAsync("uploads/large-file.zip", fileStream);

// Download as stream
using var downloadStream = await storageProvider.GetStreamAsync("uploads/large-file.zip");
```

### Container Management

Organize blobs in containers:

```csharp
// Work with different containers through named providers
var publicContainer = rest.GetProvider<IStorageProvider>("public");
var privateContainer = rest.GetProvider<IStorageProvider>("private");

await publicContainer.SetAsync("public/images/logo.png", logoData);
await privateContainer.SetAsync("private/docs/confidential.pdf", docData);
```

### Azure Caching

Use Azure Blob Storage as a distributed cache:

```csharp
var cacheProvider = rest.GetProvider<ICacheProvider>();

// Cache with expiry
await cacheProvider.SetAsync("user:123", userData, TimeSpan.FromHours(1));

// Retrieve cached data
var user = await cacheProvider.GetAsync<User>("user:123");

// Check cache existence
bool exists = await cacheProvider.ExistsAsync("user:123");
```

## Integration Patterns

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IStorageProvider>(provider =>
{
    var connectionString = configuration.GetConnectionString("AzureStorage");
    var config = new RestConfig();
    return new AzureStorageProvider(connectionString, config);
});
```

### Document Storage Service

```csharp
public class AzureDocumentService
{
    private readonly IStorageProvider _storage;

    public AzureDocumentService(IStorageProvider storage)
    {
        _storage = storage;
    }

    public async Task<string> StoreDocumentAsync(string fileName, byte[] content, string category = "general")
    {
        var blobName = $"{category}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{fileName}";
        await _storage.SetAsync(blobName, content);
        return blobName;
    }

    public async Task<byte[]> GetDocumentAsync(string blobName)
    {
        return await _storage.GetAsync<byte[]>(blobName);
    }
}
```

## Advanced Features

### Blob Properties and Metadata

```csharp
// Store with custom metadata
var metadata = new Dictionary<string, string>
{
    ["ContentType"] = "application/pdf",
    ["UploadedBy"] = "user123",
    ["Category"] = "invoices"
};

await storageProvider.SetWithMetadataAsync("invoices/inv-001.pdf", pdfData, metadata);

// Retrieve with metadata
var (data, meta) = await storageProvider.GetWithMetadataAsync<byte[]>("invoices/inv-001.pdf");
```

### Access Control

```csharp
// Configure container access levels
var config = new RestConfig
{
    AuthKey = "account",
    AuthSecret = "key",
    InstanceName = "public-container",
    // Additional Azure-specific configuration
    AzureAccessLevel = "Container" // Container, Blob, or Private
};
```

### Error Handling

```csharp
try
{
    var data = await storageProvider.GetAsync<Document>("missing-blob");
}
catch (OEliteException ex) when (ex.Message.Contains("BlobNotFound"))
{
    _logger.LogWarning("Blob not found: {BlobName}", blobName);
    return null;
}
catch (OEliteException ex) when (ex.Message.Contains("AuthenticationFailed"))
{
    _logger.LogError("Azure authentication failed: {Error}", ex.Message);
    throw;
}
```

## Performance Considerations

### Large File Handling

```csharp
// Use streams for large files
public async Task UploadLargeFileAsync(string filePath, string blobName)
{
    using var fileStream = File.OpenRead(filePath);
    await _storage.SetStreamAsync(blobName, fileStream);
}

// Download with progress tracking
public async Task DownloadWithProgressAsync(string blobName, string outputPath)
{
    using var blobStream = await _storage.GetStreamAsync(blobName);
    using var outputStream = File.Create(outputPath);

    var buffer = new byte[81920]; // 80KB buffer
    int totalBytesRead = 0;
    int bytesRead;

    while ((bytesRead = await blobStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        await outputStream.WriteAsync(buffer, 0, bytesRead);
        totalBytesRead += bytesRead;

        // Report progress
        OnProgressChanged?.Invoke(totalBytesRead);
    }
}
```

### Naming Best Practices

```csharp
// Good: Hierarchical structure
$"users/{userId}/documents/{year}/{month}/{fileName}"
$"products/{productId}/images/{imageType}/{fileName}"
$"logs/{date:yyyy/MM/dd}/{logType}.txt"

// Avoid: Flat structure
$"user_doc_{userId}_{fileName}"
```

## Integration with OElite Platform

### With OElite.Common Configuration

```csharp
// In appsettings.json
{
  "oelite": {
    "storage": {
      "azure": {
        "app": "DefaultEndpointsProtocol=https;AccountName=myaccount;..."
      }
    }
  }
}

// In your service
public class FileService
{
    private readonly IStorageProvider _azureStorage;

    public FileService(IAppConfig appConfig)
    {
        _azureStorage = appConfig.AzureStorage;
    }
}
```

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **Azure.Storage.Blobs 12.19.0+**
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

AzureStorageProvider is thread-safe and designed for concurrent operations:

- Azure SDK clients are thread-safe
- Can be used as a singleton in DI containers
- Supports parallel uploads and downloads

## License

Copyright © Phanes Technology Ltd. All rights reserved.