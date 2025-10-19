# OElite.Restme

A powerful, modular .NET library that provides a unified interface for HTTP requests, caching, message queuing, and cloud storage operations. Built with modern .NET and designed for simplicity, performance, and flexibility.

## 🚀 Features

- **Unified API**: Single `Rest` class for all operations
- **Modular Architecture**: Load only the backends you need
- **Multiple Backend Support**: Redis, RabbitMQ, Azure Blob Storage, S3-compatible providers
- **Cache Providers**: Redis, Azure Blob Storage, S3 (perfect for CDN scenarios)
- **S3-Compatible**: Support for Backblaze B2, MinIO, DigitalOcean Spaces, and more
- **Async/Await**: Full async support throughout
- **Dynamic Loading**: Providers loaded automatically when packages are referenced
- **Type Safety**: Strong typing with generic methods
- **JSON Serialization**: Built-in JSON handling with custom serialization support
- **Stream Support**: Direct stream handling for file operations
- **Expiry Support**: TTL for cache operations
- **CDN Ready**: Proper cache headers for CDN integration

## 📦 NuGet Packages

### Core Package
```bash
Install-Package OElite.Restme
```

### Backend Providers (Optional)
```bash
# Redis for caching and queuing
Install-Package OElite.Restme.Redis

# RabbitMQ for message queuing
Install-Package OElite.Restme.RabbitMQ

# Azure Blob Storage for storage and caching
Install-Package OElite.Restme.Azure

# S3-compatible storage (AWS S3, Backblaze B2, MinIO, etc.)
Install-Package OElite.Restme.S3
```

## 🏗️ Architecture

OElite.Restme uses a modular architecture where the core library provides abstractions and backend-specific implementations are loaded dynamically:

```
OElite.Restme (Core)
├── Abstractions (ICacheProvider, IStorageProvider, etc.)
├── Default Providers (Fallback implementations)
└── Service Locator (Dynamic provider loading)

Backend Packages:
├── OElite.Restme.Redis (Redis cache provider)
├── OElite.Restme.RabbitMQ (RabbitMQ queue provider)
├── OElite.Restme.Azure (Azure Blob Storage provider)
└── OElite.Restme.S3 (S3-compatible storage provider)
```

## 🚀 Quick Start

### 1. HTTP Client (No additional packages required)

```csharp
using OElite;

// Initialize HTTP client
var rest = new Rest("https://api.example.com", new RestConfig { OperationMode = RestMode.Http });

// GET request
var user = await rest.GetAsync<User>("/users/123");

// POST request with data
var newUser = await rest.PostAsync<User>("/users", userData);

// PUT request
var updatedUser = await rest.PutAsync<User>("/users/123", updatedData);

// DELETE request
await rest.DeleteAsync("/users/123");
```

### 2. Redis Caching

```csharp
// Add OElite.Restme.Redis package
var rest = new Rest("localhost:6379", RestMode.RedisCacheClient);

// Cache data with expiry
await rest.CachemeAsync("user:123", userData, 60); // 60 minutes

// Retrieve cached data
var cachedUser = await rest.FindmeAsync<User>("user:123");

// Remove from cache
await rest.RemovemeAsync("user:123");
```

### 3. Azure Blob Storage

```csharp
// Add OElite.Restme.Azure package
var connectionString = "DefaultEndpointsProtocol=https;AccountName=...;RootPath=my-app/uploads";
var rest = new Rest(connectionString, RestMode.AzureStorageClient);

// Store data (rootPath is automatically prefixed)
await rest.StoremAsync("documents/report.pdf", fileData); // Stored as "my-app/uploads/documents/report.pdf"

// Retrieve data
var fileData = await rest.RetrievemeAsync<byte[]>("documents/report.pdf");

// Use as cache (CDN-ready)
await rest.CachemeAsync("cache:key", data, TimeSpan.FromHours(1));
```

### 4. S3-Compatible Storage

```csharp
// Add OElite.Restme.S3 package

// Amazon S3
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;Region=us-west-2", RestMode.S3Client);

// Backblaze B2 with root path
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;ServiceUrl=https://s3.us-west-004.backblazeb2.com;ForcePathStyle=true;RootPath=my-app/uploads", RestMode.S3Client);

// MinIO (local development) with root path
var rest = new Rest("AccessKeyId=minioadmin;SecretAccessKey=minioadmin;ServiceUrl=http://localhost:9000;ForcePathStyle=true;UseHttp=true;RootPath=dev/cache", RestMode.S3Client);

// Store and cache operations (rootPath is automatically prefixed)
await rest.StoremAsync("files/document.pdf", fileData); // Stored as "my-app/uploads/files/document.pdf"
await rest.CachemeAsync("cache:key", data, TimeSpan.FromHours(2)); // Cached as "dev/cache/cache:key"
```

### 5. RabbitMQ Message Queuing

```csharp
// Add OElite.Restme.RabbitMQ package
var rest = new Rest("amqp://localhost", RestMode.RabbitMq);

// Publish message
await rest.QueuemeAsync("user.created", userData);

// Consume messages
await rest.DomeAsync<User>("user.created", async (user) => {
    // Process user
    return true; // Continue processing
});
```

### 6. Base Providers (No additional packages required)

Use built-in base providers for simple scenarios without external infrastructure.

```csharp
using OElite;

// Memory cache
var restMemoryCache = new Rest(
    configuration: new RestConfig { OperationMode = RestMode.MemoryAsCache }
);
await restMemoryCache.CachemeAsync("user:123", userData, TimeSpan.FromMinutes(30));
var cached = await restMemoryCache.FindmeAsync<User>("user:123");

// Local file system storage (uses default AppContext.BaseDirectory/restme_storage)
var restLocalFs = new Rest(
    endPointOrConnectionString: "/var/data/myapp", // optional base directory; omit to use default
    configuration: new RestConfig { OperationMode = RestMode.LocalFileSystemAsStorage }
);
await restLocalFs.StoremAsync("docs/report.pdf", fileBytes);
var file = await restLocalFs.RetrievemeAsync<byte[]>("docs/report.pdf");

// In-memory queue (single-process)
var restInMemoryQueue = new Rest(
    configuration: new RestConfig { OperationMode = RestMode.InMemoryQueue }
);
await restInMemoryQueue.QueuemeAsync("events.user.created", newUser);
await restInMemoryQueue.DomeAsync<User>("events.user.created", async user => {
    // handle user
    return true;
});
```

## 🔧 Configuration

### RestMode Options

- `RestMode.Http` - HTTP REST client
- `RestMode.RedisCacheClient` - Redis caching
- `RestMode.AzureStorageClient` - Azure Blob Storage
- `RestMode.S3Client` - S3-compatible storage
- `RestMode.RabbitMq` - RabbitMQ message queuing
- `RestMode.MemoryAsCache` - In-memory cache (base provider)
- `RestMode.LocalFileSystemAsStorage` - Local filesystem storage (base provider)
- `RestMode.InMemoryQueue` - In-process queue (base provider)

### Connection Strings

#### Redis
```
localhost:6379
redis://localhost:6379
redis://user:password@localhost:6379
```

#### Azure Blob Storage
```
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net;RootPath=my-app/uploads
```

#### S3-Compatible Providers
```
AccessKeyId=your_key;SecretAccessKey=your_secret;ServiceUrl=https://your-endpoint.com;BucketName=your-bucket;ForcePathStyle=true;RootPath=my-app/uploads
```

#### RabbitMQ
```
amqp://localhost
amqp://user:password@localhost:5672
```

## 📚 Advanced Usage

### Generic Operations

The `Rest` class provides generic operations that work across all backends:

```csharp
var rest = new Rest(connectionString, RestMode.S3Client);

// GET - retrieves from cache or storage
var data = await rest.GetAsync<MyData>("key");

// POST - stores in both cache and storage
var result = await rest.PostAsync<MyData>("key", data, TimeSpan.FromHours(1));

// PUT - updates cache and storage
var updated = await rest.PutAsync<MyData>("key", updatedData, TimeSpan.FromHours(2));

// DELETE - removes from cache and storage
await rest.DeleteAsync<MyData>("key");
```

### Stream Operations

Direct stream handling for file operations:

```csharp
// Store stream directly
using var fileStream = File.OpenRead("document.pdf");
await rest.StoremAsync("documents/report.pdf", fileStream);

// Retrieve as stream
var stream = await rest.RetrievemeAsync<Stream>("documents/report.pdf");
```

### Cache Operations

Advanced caching with expiry and CDN support:

```csharp
// Cache with custom expiry
await rest.CachemeAsync("user:123", userData, TimeSpan.FromMinutes(30));

// Check if cached
var exists = await rest.ExistsmeAsync("user:123");

// Set expiry
await rest.ExpiremeAsync("user:123", TimeSpan.FromHours(1));

// Find with callback
var user = await rest.FindmeAsync<User>("user:123", async (u) => {
    // Process user data
    return true;
});
```

### Message Queuing

Advanced message queuing patterns:

```csharp
// Publish to specific exchange
await rest.QueuemeAsync("user.created", userData, "user-events", "user.created");

// Consume with completion condition
await rest.DomeAsync<User>("user.created", 
    messageHandler: async (user) => {
        // Process user
        return true;
    },
    completionCondition: async () => {
        // Stop when no more messages
        return false;
    },
    exchangeName: "user-events",
    queueName: "user-processing"
);
```

## 🌐 S3-Compatible Providers

OElite.Restme.S3 supports numerous S3-compatible providers with advanced path management:

### RootPath Feature

The `RootPath` parameter allows you to organize your storage with logical path prefixes. When specified, all operations will automatically prefix the provided path to your object keys.

**Benefits:**
- **Environment Separation**: Use different root paths for dev/staging/production
- **Application Isolation**: Separate different applications in the same bucket
- **Logical Organization**: Group related files under common prefixes
- **Easy Migration**: Change root paths without code changes

**Example:**
```csharp
// Connection string with rootPath
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;RootPath=my-app/uploads", RestMode.S3Client);

// Operations automatically use the root path
await rest.StoremAsync("documents/file.pdf", data); // Stored as "my-app/uploads/documents/file.pdf"
await rest.CachemeAsync("user:123", userData); // Cached as "my-app/uploads/user:123"

// Root path is applied to all operations (GET, PUT, DELETE, EXISTS)
var file = await rest.RetrievemeAsync<byte[]>("documents/file.pdf"); // Retrieves from "my-app/uploads/documents/file.pdf"
```

**Connection String Parameters:**
- `AccessKeyId` - S3 access key ID
- `SecretAccessKey` - S3 secret access key
- `ServiceUrl` - S3 endpoint URL (for non-AWS providers)
- `BucketName` - S3 bucket name
- `Region` - AWS region (for AWS S3)
- `ForcePathStyle` - Use path-style URLs (required for most non-AWS providers)
- `UseHttp` - Use HTTP instead of HTTPS (for local development)
- `RootPath` - Logical prefix for all operations

**Examples:**
```
# AWS S3
AccessKeyId=AKIAIOSFODNN7EXAMPLE;SecretAccessKey=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY;Region=us-west-2

# Backblaze B2
AccessKeyId=your_key_id;SecretAccessKey=your_secret;ServiceUrl=https://s3.us-west-004.backblazeb2.com;ForcePathStyle=true;RootPath=myapp/data

# MinIO (Local)
AccessKeyId=minioadmin;SecretAccessKey=minioadmin;ServiceUrl=http://localhost:9000;ForcePathStyle=true;UseHttp=true;RootPath=dev/cache
```

## 📦 Complete Package Reference

### Core Packages

| Package | Description | Latest Version |
|---------|-------------|----------------|
| **OElite.Restme** | Core abstractions and HTTP client | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.svg)](https://www.nuget.org/packages/OElite.Restme/) |
| **OElite.Restme.Utils** | Utility extensions and helpers | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.Utils.svg)](https://www.nuget.org/packages/OElite.Restme.Utils/) |

### Backend Providers

| Package | Description | Latest Version |
|---------|-------------|----------------|
| **OElite.Restme.Redis** | Redis cache and queue provider | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.Redis.svg)](https://www.nuget.org/packages/OElite.Restme.Redis/) |
| **OElite.Restme.RabbitMQ** | RabbitMQ message queue provider | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.RabbitMQ.svg)](https://www.nuget.org/packages/OElite.Restme.RabbitMQ/) |
| **OElite.Restme.Azure** | Azure Blob Storage provider | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.Azure.svg)](https://www.nuget.org/packages/OElite.Restme.Azure/) |
| **OElite.Restme.S3** | S3-compatible storage provider | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.S3.svg)](https://www.nuget.org/packages/OElite.Restme.S3/) |
| **OElite.Restme.MongoDb** | MongoDB operations and aggregation | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.MongoDb.svg)](https://www.nuget.org/packages/OElite.Restme.MongoDb/) |

### Integration Packages

| Package | Description | Latest Version |
|---------|-------------|----------------|
| **OElite.Restme.Hosting** | ASP.NET Core integration extensions | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.Hosting.svg)](https://www.nuget.org/packages/OElite.Restme.Hosting/) |
| **OElite.Restme.RateLimiting** | Advanced rate limiting middleware | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.RateLimiting.svg)](https://www.nuget.org/packages/OElite.Restme.RateLimiting/) |
| **OElite.Restme.GoogleUtils** | Google Cloud integrations | [![NuGet](https://img.shields.io/nuget/v/OElite.Restme.GoogleUtils.svg)](https://www.nuget.org/packages/OElite.Restme.GoogleUtils/) |

## 🏗️ Package Details

### OElite.Restme.MongoDb

Advanced MongoDB library with comprehensive query capabilities, aggregation pipelines, and denormalization system.

**Key Features:**
- Type-safe MongoDB operations with full IntelliSense support
- Advanced aggregation pipelines for complex queries
- Automatic denormalization for efficient data relationships
- Enhanced LINQ expression support with nested documents
- Performance-optimized aggregation methods
- MongoDB class mapping with conflict resolution

**Quick Example:**
```csharp
// Entity with denormalized fields
[DbCollection("products", DbNamingConvention.SnakeCase)]
public class Product : BaseEntity
{
    [DbId] public DbObjectId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DbObjectId CategoryId { get; set; }

    [DenormalizedField("categories", "name", "@CategoryId")]
    public string CategoryName { get; set; } = string.Empty;
}

// Repository with LINQ support
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> ProductStock => new(_adapter.GetCollection<Product>());

    public async Task<List<Product>> GetExpensiveProductsAsync(decimal minPrice)
    {
        return await ProductStock
            .Where(p => p.Price > minPrice)
            .Where(p => p.Name.IsNotNullOrEmpty()) // Extension method support
            .OrderByDescending(p => p.Price)
            .Take(10)
            .ToListAsync();
    }
}
```

### OElite.Restme.Hosting

ASP.NET Core integration extensions providing dependency injection, cache adapters, and middleware.

**Key Features:**
- IDistributedCache and IMemoryCache adapters
- Dependency injection extensions for all providers
- Clean separation between core and framework integrations
- Multi-framework support (.NET 8+)
- Redis and Memory cache implementations

**Quick Example:**
```csharp
// Replace Microsoft.Extensions.Caching.StackExchangeRedis
builder.Services.AddRestmeRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
    options.InstanceName = "myapp:";
});

// Or use memory cache with both IMemoryCache and IDistributedCache
builder.Services.AddRestmeMemoryCacheWithDistributed("myapp:");

// Use in controllers exactly like Microsoft's caching
public class ProductController : ControllerBase
{
    private readonly IDistributedCache _cache;

    public ProductController(IDistributedCache cache) => _cache = cache;

    public async Task<Product> GetAsync(int id)
    {
        var cached = await _cache.GetStringAsync($"product:{id}");
        if (cached != null) return JsonSerializer.Deserialize<Product>(cached);

        // Fetch and cache...
    }
}
```

### OElite.Restme.RateLimiting

Enterprise-grade rate limiting middleware with advanced DDoS protection and adaptive limiting.

**Key Features:**
- Multiple algorithms: Fixed Window, Token Bucket, Sliding Window, Leaky Bucket
- DDoS protection with progressive blocking
- Emergency mode for severe attacks
- Adaptive limiting based on server load
- Distributed Redis storage for multi-instance deployments
- RFC 6585 compliant with proper HTTP headers

**Quick Example:**
```csharp
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.EnableDDoSProtection = true;
    options.DDoSThreshold = 5000;
    options.EmergencyMode.Enabled = true;
    options.Algorithm = RateLimitAlgorithm.TokenBucket;
    options.StorageType = RateLimitStorageType.Redis;
});

app.UseRateLimiting();
```

### OElite.Restme.Utils

Utility extensions and helper methods for common operations.

**Key Features:**
- String extension methods (`IsNotNullOrEmpty()`, etc.)
- Collection helpers and LINQ extensions
- Validation utilities
- Data transformation helpers

### OElite.Restme.GoogleUtils

Google Cloud Platform integrations and utilities.

**Key Features:**
- Google Cloud Storage integration
- Google Authentication helpers
- Firebase utilities
- Google API client extensions

## 🔧 Advanced Integration Patterns

### ASP.NET Core Cache Extensions

The Hosting package provides powerful cache extensions that work with both IDistributedCache and IMemoryCache:

```csharp
// Enhanced cache methods with Rest-style API
public static class CacheExtensions
{
    // Find cached data with advanced features
    public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
        string key,
        bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null,
        CancellationToken cancellationToken = default) where T : class;

    // Cache data with expiry and grace period
    public static async Task CachemeAsync<T>(this IDistributedCache cache,
        string key,
        T data,
        int expiryInSeconds = -1,
        int graceInSeconds = -1,
        CancellationToken cancellationToken = default) where T : class;

    // Expire cached data with grace period options
    public static async Task<bool> ExpiremeAsync(this IDistributedCache cache,
        string key,
        bool invalidateGracePeriod = true,
        CancellationToken cancellationToken = default);

    // Query object-based caching with MD5 key generation
    public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
        object queryObject,
        bool returnExpired = false,
        bool returnInGrace = true,
        Func<T, Task<bool>>? additionalValidation = null,
        Func<Task<T>>? refreshAction = null,
        CancellationToken cancellationToken = default) where T : class;
}
```

**Usage Example:**
```csharp
public class ProductService
{
    private readonly IDistributedCache _cache;

    public ProductService(IDistributedCache cache) => _cache = cache;

    public async Task<Product?> GetProductAsync(int productId)
    {
        // Try cache first with refresh callback
        return await _cache.FindmeAsync<Product>(
            key: $"product:{productId}",
            refreshAction: async () => await FetchProductFromDatabase(productId)
        );
    }

    public async Task<List<Product>> SearchProductsAsync(ProductSearchQuery query)
    {
        // Use query object for automatic MD5 key generation
        return await _cache.FindmeAsync<List<Product>>(
            queryObject: query,
            refreshAction: async () => await ExecuteProductSearch(query)
        );
    }

    public async Task InvalidateProductCacheAsync(int productId)
    {
        // Expire with grace period (allows stale data during refresh)
        await _cache.ExpiremeAsync($"product:{productId}", invalidateGracePeriod: false);
    }
}
```

## 🚀 Getting Started Scenarios

### Scenario 1: Simple Web API with Caching
```bash
# Install packages
dotnet add package OElite.Restme.Hosting
dotnet add package OElite.Restme.Redis
```

```csharp
// Program.cs
builder.Services.AddRestmeRedisCache("localhost:6379", "myapi:");

// Controller
public class ProductController : ControllerBase
{
    private readonly IDistributedCache _cache;

    [HttpGet("{id}")]
    public async Task<Product?> Get(int id)
    {
        return await _cache.FindmeAsync<Product>(
            $"product:{id}",
            refreshAction: () => _repository.GetByIdAsync(id)
        );
    }
}
```

### Scenario 2: Enterprise API with Rate Limiting and MongoDB
```bash
# Install packages
dotnet add package OElite.Restme.MongoDb
dotnet add package OElite.Restme.RateLimiting
dotnet add package OElite.Restme.Hosting
```

```csharp
// Program.cs
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 1000;
    options.EnableDDoSProtection = true;
    options.StorageType = RateLimitStorageType.Redis;
});

builder.Services.AddRestmeRedisCache("localhost:6379", "api:");

// Repository with MongoDB
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> Products => new(_adapter.GetCollection<Product>());

    public async Task<List<Product>> GetPopularProductsAsync()
    {
        return await Products
            .Where(p => p.Status == EntityStatus.Active)
            .Where(p => p.Rating > 4.0)
            .OrderByDescending(p => p.SalesCount)
            .Take(20)
            .FetchAsync<Product, ProductCollection>();
    }
}
```

### Scenario 3: Microservice with Multiple Storage Backends
```bash
# Install packages
dotnet add package OElite.Restme
dotnet add package OElite.Restme.S3
dotnet add package OElite.Restme.RabbitMQ
dotnet add package OElite.Restme.Redis
```

```csharp
public class DocumentService
{
    private readonly Rest _storage;   // S3 for file storage
    private readonly Rest _cache;     // Redis for caching
    private readonly Rest _queue;     // RabbitMQ for events

    public DocumentService()
    {
        _storage = new Rest("AccessKeyId=...;ServiceUrl=https://s3.amazonaws.com", RestMode.S3Client);
        _cache = new Rest("localhost:6379", RestMode.RedisCacheClient);
        _queue = new Rest("amqp://localhost", RestMode.RabbitMq);
    }

    public async Task<byte[]> GetDocumentAsync(string documentId)
    {
        // Try cache first
        var cached = await _cache.FindmeAsync<byte[]>($"doc:{documentId}");
        if (cached != null) return cached;

        // Fetch from storage
        var document = await _storage.RetrievemeAsync<byte[]>($"documents/{documentId}");

        // Cache for future requests
        await _cache.CachemeAsync($"doc:{documentId}", document, TimeSpan.FromHours(1));

        return document;
    }

    public async Task SaveDocumentAsync(string documentId, byte[] data)
    {
        // Save to storage
        await _storage.StoremAsync($"documents/{documentId}", data);

        // Invalidate cache
        await _cache.RemovemeAsync($"doc:{documentId}");

        // Publish event
        await _queue.QueuemeAsync("document.saved", new { DocumentId = documentId });
    }
}
```

## 🏆 Best Practices

### 1. Package Selection
- **Core only**: Use `OElite.Restme` for HTTP clients
- **ASP.NET Core**: Add `OElite.Restme.Hosting` for DI integration
- **Caching**: Use `OElite.Restme.Redis` or memory providers
- **Database**: Add `OElite.Restme.MongoDb` for MongoDB operations
- **Security**: Include `OElite.Restme.RateLimiting` for protection

### 2. Configuration Management
```csharp
// Use configuration sections
builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection("Redis")
);

// Environment-based configuration
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION");
```

### 3. Error Handling and Resilience
```csharp
// Graceful degradation for cache failures
public async Task<Product?> GetProductAsync(int id)
{
    try
    {
        return await _cache.FindmeAsync<Product>($"product:{id}");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cache error for product {ProductId}", id);
        return await _repository.GetByIdAsync(id); // Fallback to database
    }
}
```

### 4. Performance Optimization
```csharp
// Use appropriate data structures
public async Task<ProductCollection> GetProductsByCategoryAsync(int categoryId)
{
    return await Products
        .Where(p => p.CategoryId == categoryId)
        .Where(p => p.Status == EntityStatus.Active)
        .OrderBy(p => p.Name)
        .FetchAsync<Product, ProductCollection>(returnTotalCount: false); // Skip count for better performance
}
```

## 🔗 Related Resources

- **Documentation**: [OElite Platform Wiki](https://wiki.oelite.com)
- **Examples**: [GitHub Examples Repository](https://github.com/oelite/examples)
- **API Reference**: [API Documentation](https://docs.oelite.com/restme)
- **Support**: [GitHub Issues](https://github.com/oelite/uranus/restme/issues)

## 📄 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

---

**OElite.Restme** - *Unifying your data operations across HTTP, caching, storage, and messaging*