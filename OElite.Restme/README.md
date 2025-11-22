# OElite.Restme

**RESTful HTTP client, storage, caching, and integration toolkit for .NET applications.**

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme)](https://www.nuget.org/packages/OElite.Restme/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)

## Overview

OElite.Restme is the foundational toolkit that provides unified access to external services including RESTful APIs, cloud storage (S3-compatible), Redis caching, and message queuing. It's the core abstraction layer that powers all OElite platform integrations and is heavily used throughout the `OElite.Common` configuration system.

## Features

### 🌐 Universal REST Client
- **RestMode Support**: HTTP, Redis, S3, RabbitMQ operations through unified interface
- **Generic Provider Pattern**: Type-safe provider access with `GetProvider<T>()`
- **Capability Detection**: Automatic provider capability checking before instantiation
- **Async/Await Patterns**: Modern async programming support
- **Connection Pooling**: Efficient resource management
- **Timeout Management**: Configurable timeout handling
- **Error Resilience**: Built-in retry mechanisms and failure handling

### 🔧 Multi-Protocol Support
- **RestMode.Http**: Standard RESTful API calls
- **RestMode.Redis**: Redis operations for caching
- **RestMode.S3**: S3-compatible storage operations
- **RestMode.RabbitMq**: Message queue operations
- **RestMode.ClickHouse**: Columnar database operations
- **RestMode.Kafka**: Streaming data operations
- **RestMode.OpenSearch**: Full-text search operations
- **RestMode.MongoDb**: MongoDB operations (when used with OElite.Restme.MongoDb)

### ⚡ Performance Optimized
- **Connection Reuse**: Efficient HTTP client management
- **Memory Management**: Optimized for high-throughput scenarios
- **Streaming Support**: Large file handling capabilities
- **Compression**: Built-in response compression support

## Quick Start

### 1. Installation

```bash
dotnet add package OElite.Restme
```

### 2. Generic Provider Pattern (Recommended)

```csharp
using OElite;
using OElite.Abstractions;

// Create Rest instance with automatic provider factory
var rest = new Rest("https://api.example.com",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Http,
        DefaultTimeout = 30000
    });

// Get provider using generic pattern
var httpProvider = rest.GetProvider<IHttpProvider>();
var cacheProvider = rest.GetProvider<ICacheProvider>(); // Returns null if not available

// Check provider capabilities before use
if (httpProvider != null)
{
    // Use HTTP provider
    var response = await rest.GetAsync<ApiResponse>("/users/123");
}
```

### 3. Basic HTTP Client Usage

```csharp
using OElite.Restme;

// Create REST client for API calls
var apiClient = new Rest("https://api.example.com",
    configuration: new RestConfig
    {
        DefaultTimeout = 30000,
        OperationMode = RestMode.Http
    });

// GET request
var response = await apiClient.GetAsync<ApiResponse>("/users/123");

// POST request
var createUser = new CreateUserRequest { Name = "John", Email = "john@example.com" };
var newUser = await apiClient.PostAsync<User>("/users", createUser);

// PUT request with custom headers
var headers = new Dictionary<string, string>
{
    ["Authorization"] = "Bearer your-token",
    ["Content-Type"] = "application/json"
};
var updatedUser = await apiClient.PutAsync<User>("/users/123", updateData, headers);
```

### 4. Redis Cache Operations

```csharp
// Create Redis client with new provider pattern
var redisRest = new Rest("redis://localhost:6379",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Redis,
        DefaultTimeout = 5000
    });

// Access cache provider using generic pattern
var cacheProvider = redisRest.GetProvider<ICacheProvider>();

if (cacheProvider != null)
{
    // Cache operations via provider
    await cacheProvider.SetAsync("user:123", userObject, TimeSpan.FromMinutes(30));
    var cachedUser = await cacheProvider.GetAsync<User>("user:123");
    await cacheProvider.RemoveAsync("user:123");
}

// Or use convenience methods (backward compatible)
await redisRest.Put("user:123", userObject, TimeSpan.FromMinutes(30));
var cachedUser = await redisRest.Get<User>("user:123");
```

### 5. S3 Storage Operations

```csharp
// Create S3 client with new provider pattern
var s3Rest = new Rest("https://s3.amazonaws.com",
    configuration: new RestConfig
    {
        OperationMode = RestMode.S3,
        AuthKey = "your-access-key",
        AuthSecret = "your-secret-key"
    });

// Access storage provider using generic pattern
var storageProvider = s3Rest.GetProvider<IStorageProvider>();

if (storageProvider != null)
{
    // Storage operations via provider
    await storageProvider.PutAsync("documents/document.pdf", pdfBytes);
    var file = await storageProvider.GetAsync<byte[]>("documents/document.pdf");
    await storageProvider.DeleteAsync("documents/document.pdf");
}

// Or use cache provider (S3 supports both!)
var cacheProvider = s3Rest.GetProvider<ICacheProvider>();
if (cacheProvider != null)
{
    await cacheProvider.SetAsync("cache:key", data, TimeSpan.FromHours(1));
}
```

## Provider Factory System

### Generic Provider Pattern

OElite.Restme uses a modern factory pattern with automatic capability detection and supports **named providers** for advanced use cases:

```csharp
// Create Rest instance
var rest = new Rest(connectionString, new RestConfig
{
    OperationMode = RestMode.Redis
});

// Get provider using type-safe generic pattern
var cacheProvider = rest.GetProvider<ICacheProvider>();
var storageProvider = rest.GetProvider<IStorageProvider>();
var queueProvider = rest.GetProvider<IQueueProvider>();
var columnarProvider = rest.GetProvider<IColumnarProvider>();
var streamingProvider = rest.GetProvider<IStreamingProvider>();
var searchProvider = rest.GetProvider<ISearchProvider>();

// NEW: Named providers for multiple instances of the same type
var primaryCache = rest.GetProvider<ICacheProvider>("primary");
var secondaryCache = rest.GetProvider<ICacheProvider>("secondary");
var analyticsCache = rest.GetProvider<ICacheProvider>("analytics");

// Providers return null if not supported by current mode
if (cacheProvider != null)
{
    await cacheProvider.SetAsync("key", value);
}

// Named providers enable complex architectures
if (primaryCache != null && secondaryCache != null)
{
    // Use different cache instances for different purposes
    await primaryCache.SetAsync("user:123", userData);
    await secondaryCache.SetAsync("session:456", sessionData);
}
```

### Provider Capabilities

Each provider mode supports specific capabilities:

| RestMode | Cache | Storage | Queue | Columnar | Streaming | Search |
|----------|-------|---------|-------|----------|-----------|--------|
| **Redis** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **S3** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Azure** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **RabbitMQ** | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| **ClickHouse** | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Kafka** | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ |
| **OpenSearch** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

### Capability Detection

```csharp
// Check capabilities before requesting provider
var factory = ServiceLocator.GetFactory("redis");

if (factory != null)
{
    // Check supported capabilities
    var capabilities = factory.SupportedCapabilities;
    Console.WriteLine($"Supports Cache: {capabilities.HasFlag(ProviderCapabilities.Cache)}");
    Console.WriteLine($"Supports Storage: {capabilities.HasFlag(ProviderCapabilities.Storage)}");
    
    // Check if specific provider type can be created
    if (factory.CanCreateProvider<ICacheProvider>())
    {
        var provider = factory.CreateProvider<ICacheProvider>(connectionString, config);
    }
}
```

### Multi-Provider Usage

Use multiple providers simultaneously for different capabilities:

```csharp
// Redis for caching
var redisRest = new Rest("redis://localhost:6379",
    new RestConfig { OperationMode = RestMode.Redis });
var cache = redisRest.GetProvider<ICacheProvider>();

// S3 for storage
var s3Rest = new Rest("https://s3.amazonaws.com",
    new RestConfig { OperationMode = RestMode.S3, AuthKey = "key", AuthSecret = "secret" });
var storage = s3Rest.GetProvider<IStorageProvider>();

// RabbitMQ for queuing
var rabbitRest = new Rest("amqp://localhost:5672",
    new RestConfig { OperationMode = RestMode.RabbitMq });
var queue = rabbitRest.GetProvider<IQueueProvider>();

// Use together
await cache.SetAsync("data:123", data, TimeSpan.FromMinutes(15));
await storage.PutAsync("backups/data-123.json", data);
await queue.PublishAsync("data.events", new DataCreatedEvent { Id = "123" });
```

### Named Providers

**NEW in v2.1.0**: Support for multiple provider instances of the same type using named providers:

```csharp
// Create multiple Rest instances for different purposes
var mainRest = new Rest("redis://main.cache:6379", new RestConfig { OperationMode = RestMode.Redis });
var sessionRest = new Rest("redis://session.cache:6379", new RestConfig { OperationMode = RestMode.Redis });
var analyticsRest = new Rest("redis://analytics.cache:6379", new RestConfig { OperationMode = RestMode.Redis });

// Get named providers for different use cases
var mainCache = mainRest.GetProvider<ICacheProvider>("primary");
var sessionCache = sessionRest.GetProvider<ICacheProvider>("sessions");
var analyticsCache = analyticsRest.GetProvider<ICacheProvider>("analytics");

// Use different cache instances for different purposes
await mainCache.SetAsync("user:123", userData, TimeSpan.FromMinutes(30));
await sessionCache.SetAsync("session:abc", sessionData, TimeSpan.FromHours(24));
await analyticsCache.SetAsync("metrics:daily", metricsData, TimeSpan.FromDays(1));

// Default provider (backward compatible)
var defaultCache = mainRest.GetProvider<ICacheProvider>(); // Same as GetProvider<ICacheProvider>("default")

// Named providers enable clean separation of concerns
public class CacheService
{
    private readonly ICacheProvider _userCache;
    private readonly ICacheProvider _sessionCache;
    private readonly ICacheProvider _tempCache;

    public CacheService()
    {
        var rest = new Rest(connectionString, config);
        _userCache = rest.GetProvider<ICacheProvider>("users");
        _sessionCache = rest.GetProvider<ICacheProvider>("sessions");
        _tempCache = rest.GetProvider<ICacheProvider>("temp");
    }

    public async Task CacheUserDataAsync(string userId, object userData)
    {
        await _userCache.SetAsync($"user:{userId}", userData, TimeSpan.FromMinutes(30));
    }

    public async Task CacheSessionAsync(string sessionId, object sessionData)
    {
        await _sessionCache.SetAsync($"session:{sessionId}", sessionData, TimeSpan.FromHours(24));
    }

    public async Task CacheTempDataAsync(string key, object data)
    {
        await _tempCache.SetAsync(key, data, TimeSpan.FromMinutes(5));
    }
}
```

## Advanced Usage

### Configuration in OElite Applications

OElite.Restme integrates seamlessly with OElite.Common configuration:

```csharp
// In your app configuration (appsettings.json)
{
  "oelite": {
    "data": {
      "redis": {
        "app": "redis://localhost:6379/0"
      }
    },
    "storage": {
      "s3": {
        "app": "https://s3.amazonaws.com/my-app-bucket"
      }
    }
  }
}

// Access through OElite.Common.Infrastructure.BaseAppConfig
public class MyAppConfig : BaseAppConfig
{
    public MyAppConfig(string jsonConfig) : base(OeAppType.GeneralWebApp, jsonConfig) { }
}

// Usage in services
public class FileService
{
    private readonly Rest _storage;
    private readonly Rest _cache;

    public FileService(IAppConfig appConfig)
    {
        _storage = appConfig.Storage;    // S3 client
        _cache = appConfig.RedisCache;   // Redis client
    }

    public async Task<string> ProcessFileAsync(string fileName)
    {
        // Check cache first
        var cached = await _cache.GetAsync<ProcessedFile>($"processed:{fileName}");
        if (cached != null) return cached.Result;

        // Download from S3
        using var fileStream = await _storage.DownloadAsync($"uploads/{fileName}");

        // Process file...
        var result = await ProcessFileContent(fileStream);

        // Cache result
        await _cache.SetAsync($"processed:{fileName}",
            new ProcessedFile { Result = result },
            TimeSpan.FromHours(1));

        return result;
    }
}
```

### Error Handling and Resilience

```csharp
var client = new Rest("https://api.example.com",
    configuration: new RestConfig
    {
        DefaultTimeout = 30000,
        MaxRetryAttempts = 3,
        RetryDelayMs = 1000,
        OperationMode = RestMode.Http
    });

try
{
    var result = await client.GetAsync<ApiResponse>("/data");
}
catch (RestmeTimeoutException ex)
{
    // Handle timeout
    logger.LogWarning("API call timed out: {Message}", ex.Message);
}
catch (RestmeHttpException ex)
{
    // Handle HTTP errors
    logger.LogError("API call failed: {StatusCode} - {Message}",
        ex.StatusCode, ex.Message);
}
catch (RestmeConnectionException ex)
{
    // Handle connection issues
    logger.LogError("Connection failed: {Message}", ex.Message);
}
```

### Custom Serialization and Headers

```csharp
var client = new Rest("https://api.example.com",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Http,
        CustomHeaders = new Dictionary<string, string>
        {
            ["User-Agent"] = "MyApp/1.0",
            ["Accept"] = "application/json",
            ["X-API-Version"] = "v2"
        },
        SerializationSettings = new JsonSerializerSettings
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true
        }
    });

// Custom request with additional headers
var customHeaders = new Dictionary<string, string>
{
    ["Authorization"] = $"Bearer {accessToken}",
    ["X-Request-ID"] = Guid.NewGuid().ToString()
};

var response = await client.GetAsync<ApiResponse>("/protected-data", customHeaders);
```

### Streaming and Large File Handling

```csharp
// Upload large file with progress tracking
using var fileStream = File.OpenRead("large-file.zip");

var s3Client = new Rest("https://s3.amazonaws.com/uploads",
    configuration: new RestConfig
    {
        OperationMode = RestMode.S3AsStorage,
        UploadChunkSize = 1024 * 1024 * 10 // 10MB chunks
    });

await s3Client.UploadAsync("files/large-file.zip", fileStream,
    contentType: "application/zip",
    progressCallback: (bytesTransferred, totalBytes) =>
    {
        var percentage = (double)bytesTransferred / totalBytes * 100;
        Console.WriteLine($"Upload progress: {percentage:F1}%");
    });

// Download with streaming
using var downloadStream = await s3Client.DownloadStreamAsync("files/large-file.zip");
using var outputStream = File.Create("downloaded-file.zip");

await downloadStream.CopyToAsync(outputStream);
```

## RestMode Operations Reference

### HTTP Mode (RestMode.Http)

```csharp
// Standard HTTP operations
var response = await client.GetAsync<T>(path, headers);
var result = await client.PostAsync<T>(path, data, headers);
var updated = await client.PutAsync<T>(path, data, headers);
var deleted = await client.DeleteAsync(path, headers);

// Raw string responses
var rawResponse = await client.GetStringAsync(path, headers);
var rawPost = await client.PostStringAsync(path, jsonData, headers);
```

### Redis Mode (RestMode.RedisAsCache)

```csharp
// Cache operations
await client.SetAsync(key, value, expiry);
var value = await client.GetAsync<T>(key);
await client.DeleteAsync(key);
var exists = await client.ExistsAsync(key);

// Redis collections
await client.SetAddAsync(setKey, value);
var setMembers = await client.SetMembersAsync<T>(setKey);
await client.HashSetAsync(hashKey, field, value);
var hashValue = await client.HashGetAsync<T>(hashKey, field);
```

### S3 Mode (RestMode.S3AsStorage)

```csharp
// File operations
await client.UploadAsync(key, stream, contentType);
using var downloadStream = await client.DownloadAsync(key);
await client.DeleteAsync(key);
var exists = await client.ExistsAsync(key);

// Metadata operations
var metadata = await client.GetMetadataAsync(key);
var objects = await client.ListObjectsAsync(prefix);

// URL operations
var downloadUrl = await client.GetDownloadUrlAsync(key, TimeSpan.FromHours(1));
var uploadUrl = await client.GetUploadUrlAsync(key, TimeSpan.FromMinutes(30));
```

### RabbitMQ Mode (RestMode.RabbitMq)

```csharp
// Message operations
await client.PublishAsync(queueName, message);
var messages = await client.ConsumeAsync<T>(queueName, maxMessages: 10);
await client.AcknowledgeAsync(queueName, messageId);

// Queue management
await client.CreateQueueAsync(queueName, durable: true);
await client.DeleteQueueAsync(queueName);
var queueInfo = await client.GetQueueInfoAsync(queueName);
```

## Integration with OElite Platform

### Automatic Configuration

When using `OElite.Common.Hosting`, Rest clients are automatically configured:

```csharp
// In your service
public class ProductService : IOEliteService
{
    private readonly Rest _externalApi;
    private readonly Rest _cache;
    private readonly Rest _storage;

    public ProductService(IAppConfig appConfig)
    {
        // These are automatically configured from appsettings.json
        _cache = appConfig.RedisCache;      // Redis client
        _storage = appConfig.Storage;       // S3 client

        // Create additional clients as needed
        _externalApi = new Rest("https://external-api.com",
            configuration: new RestConfig
            {
                OperationMode = RestMode.Http,
                DefaultTimeout = 15000
            });
    }
}
```

### Dependency Injection Integration

```csharp
// Register Rest clients in DI container
services.AddSingleton<Rest>(provider =>
{
    var config = provider.GetRequiredService<IAppConfig>();
    return new Rest("https://external-service.com",
        configuration: new RestConfig
        {
            OperationMode = RestMode.Http,
            AuthKey = config.GetApiKey("external-service"),
            DefaultTimeout = 30000
        });
});

// Use named clients for multiple services
services.AddSingleton<Rest>("PaymentApi", provider =>
    new Rest("https://payments.example.com", new RestConfig
    {
        OperationMode = RestMode.Http,
        AuthKey = provider.GetRequiredService<IConfiguration>()["PaymentApi:ApiKey"]
    }));

services.AddSingleton<Rest>("NotificationApi", provider =>
    new Rest("https://notifications.example.com", new RestConfig
    {
        OperationMode = RestMode.Http
    }));
```

## Performance Best Practices

### Connection Management

```csharp
// ✅ DO: Reuse Rest instances
private static readonly Rest _sharedClient = new Rest("https://api.example.com",
    new RestConfig { OperationMode = RestMode.Http });

// ✅ DO: Use singleton registration in DI
services.AddSingleton<Rest>(/* configuration */);

// ❌ DON'T: Create new instances for every request
public async Task<ApiResponse> GetDataAsync()
{
    using var client = new Rest("https://api.example.com"); // Inefficient
    return await client.GetAsync<ApiResponse>("/data");
}
```

### Async Operations

```csharp
// ✅ DO: Use async methods properly
public async Task<List<Product>> GetProductsAsync()
{
    var response = await _client.GetAsync<ApiResponse<Product[]>>("/products");
    return response.Data.ToList();
}

// ❌ DON'T: Block async calls
public List<Product> GetProducts()
{
    var response = _client.GetAsync<ApiResponse<Product[]>>("/products").Result; // Deadlock risk
    return response.Data.ToList();
}
```

### Resource Disposal

```csharp
// ✅ DO: Dispose streams properly
public async Task ProcessFileAsync(string fileName)
{
    using var downloadStream = await _storage.DownloadAsync(fileName);
    using var processedStream = await ProcessStream(downloadStream);
    using var uploadStream = File.Create("processed-" + fileName);

    await processedStream.CopyToAsync(uploadStream);
}

// ✅ DO: Use ConfigureAwait(false) in library code
public async Task<ApiResponse> GetDataAsync()
{
    var response = await _client.GetAsync<ApiResponse>("/data").ConfigureAwait(false);
    return response;
}
```

## Troubleshooting

### Common Configuration Issues

```csharp
// Check connection string format
// ✅ Correct Redis connection strings:
"redis://localhost:6379"
"redis://username:password@localhost:6379/0"
"localhost:6379"

// ✅ Correct S3 connection strings:
"https://s3.amazonaws.com/bucket-name"
"https://minio.example.com:9000/bucket-name"

// ❌ Incorrect formats:
"redis:localhost:6379"  // Missing //
"s3://bucket-name"      // Missing HTTPS endpoint
```

### Debugging Connection Issues

```csharp
var client = new Rest("https://api.example.com",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Http,
        EnableDebugLogging = true, // Enable detailed logging
        DefaultTimeout = 30000
    });

try
{
    var response = await client.GetAsync<ApiResponse>("/test");
}
catch (RestmeException ex)
{
    Console.WriteLine($"Rest operation failed: {ex.Message}");
    Console.WriteLine($"Operation Mode: {ex.OperationMode}");
    Console.WriteLine($"Endpoint: {ex.Endpoint}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
}
```

## Version History

- **2.1.0**: Current version with major refactoring and .NET 10.0 support
  - **Generic Provider Factory Pattern**: Type-safe `GetProvider<T>()` with capability detection
  - **Capability-Based Architecture**: Factories declare supported provider types via `ProviderCapabilities` flags
  - **Removed 350+ lines** of duplicate initialization code
  - **Backward Compatible**: Existing code continues to work seamlessly
  - Enhanced async/await patterns
  - Improved connection pooling and resource management
  - Extended provider ecosystem (ClickHouse, Kafka, OpenSearch)
  - Better error handling and resilience
  - Performance optimizations for high-throughput scenarios

## Related Packages

### Provider Packages (Auto-Registered via Factory Pattern)
- **OElite.Restme.Redis**: Redis caching provider (Cache capability)
- **OElite.Restme.S3**: S3-compatible storage provider (Cache + Storage capabilities)
- **OElite.Restme.Azure**: Azure storage provider (Cache + Storage capabilities)
- **OElite.Restme.RabbitMQ**: RabbitMQ messaging provider (Queue capability)
- **OElite.Restme.ClickHouse**: ClickHouse analytics provider (Columnar capability)
- **OElite.Restme.Kafka**: Apache Kafka streaming provider (Streaming capability)
- **OElite.Restme.OpenSearch**: OpenSearch/Elasticsearch provider (Search capability)

### Core Packages
- **OElite.Restme.MongoDb**: MongoDB integration and operations
- **OElite.Restme.Hosting**: Hosting integration and service registration
- **OElite.Restme.Utils**: Utility functions and extensions
- **OElite.Common**: Configuration integration and app config patterns

## License

Copyright © Phanes Technology Ltd. All rights reserved.

## Support

For support and documentation, visit [https://www.oelite.com](https://www.oelite.com)

---

## Platform Integration

OElite.Restme is the foundational client library used throughout the OElite platform for all external integrations. It provides the unified interface that powers:
- **OElite.Common configuration**: Automatic Redis and S3 client creation
- **OElite.Services**: External API integrations in business services
- **Data synchronization**: Background sync operations with external systems
- **File management**: Asset storage and retrieval operations
- **Caching strategies**: Performance optimization across the platform