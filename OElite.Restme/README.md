# OElite.Restme

**RESTful HTTP client, storage, caching, and integration toolkit for .NET applications.**

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme)](https://www.nuget.org/packages/OElite.Restme/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)

## Overview

OElite.Restme is the foundational toolkit that provides unified access to external services including RESTful APIs, cloud storage (S3-compatible), Redis caching, and message queuing. It's the core abstraction layer that powers all OElite platform integrations and is heavily used throughout the `OElite.Common` configuration system.

## Features

### 🌐 Universal REST Client
- **RestMode Support**: HTTP, Redis, S3, RabbitMQ operations through unified interface
- **Async/Await Patterns**: Modern async programming support
- **Connection Pooling**: Efficient resource management
- **Timeout Management**: Configurable timeout handling
- **Error Resilience**: Built-in retry mechanisms and failure handling

### 🔧 Multi-protocol Support
- **RestMode.Http**: Standard RESTful API calls
- **RestMode.RedisAsCache**: Redis operations for caching
- **RestMode.S3AsStorage**: S3-compatible storage operations
- **RestMode.RabbitMq**: Message queue operations
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

### 2. Basic HTTP Client Usage

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

### 3. Redis Cache Operations

```csharp
// Create Redis client
var redisClient = new Rest("redis://localhost:6379",
    configuration: new RestConfig
    {
        OperationMode = RestMode.RedisAsCache,
        DefaultTimeout = 5000
    });

// Cache operations
await redisClient.SetAsync("user:123", userObject, TimeSpan.FromMinutes(30));
var cachedUser = await redisClient.GetAsync<User>("user:123");

// Redis-specific operations
await redisClient.DeleteAsync("user:123");
var exists = await redisClient.ExistsAsync("user:123");
```

### 4. S3 Storage Operations

```csharp
// Create S3 client
var s3Client = new Rest("https://s3.amazonaws.com/my-bucket",
    configuration: new RestConfig
    {
        OperationMode = RestMode.S3AsStorage,
        RestKey = "your-access-key",
        RestSecret = "your-secret-key"
    });

// Upload file
using var fileStream = File.OpenRead("document.pdf");
await s3Client.UploadAsync("documents/document.pdf", fileStream, "application/pdf");

// Download file
using var downloadStream = await s3Client.DownloadAsync("documents/document.pdf");

// List objects
var objects = await s3Client.ListObjectsAsync("documents/");
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
            RestKey = config.GetApiKey("external-service"),
            DefaultTimeout = 30000
        });
});

// Use named clients for multiple services
services.AddSingleton<Rest>("PaymentApi", provider =>
    new Rest("https://payments.example.com", new RestConfig
    {
        OperationMode = RestMode.Http,
        RestKey = provider.GetRequiredService<IConfiguration>()["PaymentApi:ApiKey"]
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

- **2.1.0**: Current version with .NET 10.0 support
- Enhanced async/await patterns
- Improved connection pooling and resource management
- Extended S3-compatible storage support
- Better error handling and resilience
- Performance optimizations for high-throughput scenarios

## Related Packages

- **OElite.Restme.MongoDb**: MongoDB integration and operations
- **OElite.Restme.Redis**: Enhanced Redis operations and caching
- **OElite.Restme.S3**: Extended S3-compatible storage features
- **OElite.Restme.RabbitMQ**: RabbitMQ message queue operations
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