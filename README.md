# OElite.Restme

A powerful, modular .NET library that provides a unified interface for HTTP requests, caching, message queuing, and cloud storage operations. Built with modern .NET and designed for simplicity, performance, and flexibility.

## 🚀 Features

- **Unified API**: Single `Rest` class for all operations
- **Named Provider Support**: Multiple provider instances of the same type with `GetProvider<T>(string name)`
- **Modular Architecture**: Load only the backends you need
- **Multiple Backend Support**: Redis, RabbitMQ, Azure Blob Storage, S3-compatible providers, ClickHouse, Kafka, OpenSearch
- **Cache Providers**: Redis, Azure Blob Storage, S3 (perfect for CDN scenarios)
- **Analytics & Search**: ClickHouse for time-series analytics, OpenSearch for full-text search
- **S3-Compatible**: Support for Backblaze B2, MinIO, DigitalOcean Spaces, and more
- **Async/Await**: Full async support throughout
- **Dynamic Loading**: Providers loaded automatically when packages are referenced
- **Type Safety**: Strong typing with generic methods
- **JSON Serialization**: Built-in JSON handling with custom serialization support
- **Stream Support**: Direct stream handling for file operations
- **Expiry Support**: TTL for cache operations, table TTL in ClickHouse, topic retention in Kafka, index lifecycle in OpenSearch
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

# ClickHouse for columnar analytics and time-series
Install-Package OElite.Restme.ClickHouse

# Kafka for streaming and event processing
Install-Package OElite.Restme.Kafka

# OpenSearch for search and analytics
Install-Package OElite.Restme.OpenSearch
```

## 🏗️ Architecture

OElite.Restme uses a modern **provider pattern architecture** where the core library provides abstractions and backend-specific implementations are loaded dynamically. The `Rest` class serves as an orchestrator that delegates operations to specialized providers:

```
OElite.Restme (Core)
├── Rest Class (Orchestrator)
├── Provider Abstractions (IHttpProvider, ICacheProvider, IStorageProvider, etc.)
├── HttpClientProvider (Built-in HTTP implementation)
├── Default Providers (Fallback implementations)
└── Service Locator (Dynamic provider loading)

Backend Packages:
├── OElite.Restme.Redis (Redis cache provider)
├── OElite.Restme.RabbitMQ (RabbitMQ queue provider)
├── OElite.Restme.Azure (Azure Blob Storage provider)
├── OElite.Restme.S3 (S3-compatible storage provider)
├── OElite.Restme.ClickHouse (Columnar analytics provider)
├── OElite.Restme.Kafka (Streaming provider)
└── OElite.Restme.OpenSearch (Search provider)
```

### Provider Pattern Benefits

- **🎯 Separation of Concerns**: HTTP, caching, storage, and messaging operations are handled by specialized providers
- **🔌 Extensibility**: Easy to add new providers without modifying core architecture
- **🧪 Testability**: Mock individual providers for unit testing
- **⚡ Performance**: Optimized implementations per provider type
- **🔄 Flexibility**: Mix and match providers based on requirements

### HttpClientProvider Features

The built-in **HttpClientProvider** offers enhanced HTTP functionality:

- **✅ HttpRequestContext Support**: Rich context passing with headers, parameters, BaseUri, and timeout
- **✅ Enhanced Response Processing**: `HttpResponseMessage<T>` with metadata (headers, status codes, timestamps)
- **✅ Configuration Integration**: Supports `UseRestConvertForCollectionSerialization` and custom serialization
- **✅ Content Type Handling**: Automatic JSON/Form encoding based on RestMode (Http vs HttpRest)
- **✅ Stream Support**: Handle file uploads/downloads with stream types
- **✅ Error Handling**: Comprehensive exception handling and logging support

## 🚀 Quick Start

### 1. HTTP Client (No additional packages required)

```csharp
using OElite.Restme;
using OElite.Restme.Abstractions;

// Initialize HTTP client
var rest = new Rest("https://api.example.com", new RestConfig { OperationMode = RestMode.Http });

// Get HTTP provider using modern provider pattern
var httpProvider = rest.GetProvider<IHttpProvider>();

// GET request
var user = await httpProvider.GetAsync<User>("/users/123");

// POST request with data
var newUser = await httpProvider.PostAsync<User>("/users", userData);

// PUT request
var updatedUser = await httpProvider.PutAsync<User>("/users/123", updatedData);

// DELETE request
await httpProvider.DeleteAsync<bool>("/users/123");

// Enhanced HTTP requests with full response details
var response = await httpProvider.HttpRequestFullWithDetailsAsync<User>(
    HttpMethod.Get, "/users/123");

if (response?.Data != null)
{
    Console.WriteLine($"Status: {response.StatusCode}");
    Console.WriteLine($"Headers: {response.ResponseHeaders?.Count}");
    Console.WriteLine($"User: {response.Data.Name}");
}
```

#### Alternative: Direct CRUD Operations (Backward Compatible)
```csharp
// For simple operations, you can still use direct CRUD methods
var user = await rest.GetAsync<User>("/users/123");
var newUser = await rest.PostAsync<User>("/users", userData);
var updatedUser = await rest.PutAsync<User>("/users/123", updatedData);
await rest.DeleteAsync<bool>("/users/123");
```

### 2. Redis Caching

```csharp
// Add OElite.Restme.Redis package
var rest = new Rest("localhost:6379", RestMode.Redis);

// Or use RestConfig for authentication
var config = new RestConfig
{
    AuthSecret = "your-redis-password" // For password-protected Redis
};
var rest = new Rest("localhost:6379", config, RestMode.Redis);

// Get cache provider
var cacheProvider = rest.GetProvider<ICacheProvider>();

// Cache data with expiry (using ICacheProvider directly)
await cacheProvider.SetAsync("user:123", userData, TimeSpan.FromMinutes(60));

// Cache data with expiry (using CachemeAsync extension)
var success = await cacheProvider.CachemeAsync("user:123", userData, expiryInSeconds: 3600); // 60 minutes

// Retrieve cached data (using ICacheProvider directly)
var cachedUser = await cacheProvider.GetAsync<User>("user:123");

// Retrieve cached data (using FindmeAsync extension with validation)
var cachedUserWithValidation = await cacheProvider.FindmeAsync<User>("user:123");

// Remove from cache
await cacheProvider.RemoveAsync("user:123");

// Force expiry (using ExpiremeAsync extension)
await cacheProvider.ExpiremeAsync("user:123");
```

### 3. Azure Blob Storage

```csharp
// Add OElite.Restme.Azure package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "your-account-name",      // Azure Storage Account Name
    AuthSecret = "your-account-key",    // Azure Storage Account Key
    Endpoint = "core.windows.net",      // Optional: Azure endpoint
    RootPath = "my-app/uploads"         // Optional: Logical path prefix
};
var rest = new Rest("DefaultEndpointsProtocol=https", config, RestMode.Azure);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("DefaultEndpointsProtocol=https;AccountName=...;RootPath=my-app/uploads", RestMode.Azure);

// Store data (rootPath is automatically prefixed)
await rest.SetAsync("documents/report.pdf", fileData); // Stored as "my-app/uploads/documents/report.pdf"

// Retrieve data
var fileData = await rest.GetAsync<byte[]>("documents/report.pdf");

// Use as cache (CDN-ready)
var cacheProvider = rest.GetProvider<ICacheProvider>();
await cacheProvider.SetAsync("cache:key", data, TimeSpan.FromHours(1));
```

### 4. S3-Compatible Storage

```csharp
// Add OElite.Restme.S3 package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "your-access-key-id",     // S3 Access Key ID
    AuthSecret = "your-secret-access-key", // S3 Secret Access Key
    Endpoint = "https://s3.amazonaws.com", // S3 endpoint
    Region = "us-west-2",               // AWS region
    BucketName = "my-bucket",           // S3 bucket name
    RootPath = "my-app/uploads"         // Optional: Logical path prefix
};
var rest = new Rest("s3://", config, RestMode.S3);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;Region=us-west-2", RestMode.S3);

// Backblaze B2 with RestConfig
var b2Config = new RestConfig
{
    AuthKey = "your-b2-key-id",
    AuthSecret = "your-b2-secret",
    Endpoint = "https://s3.us-west-004.backblazeb2.com",
    BucketName = "my-bucket",
    RootPath = "my-app/uploads"
};
var rest = new Rest("s3://", b2Config, RestMode.S3);

// MinIO (local development) with RestConfig
var minioConfig = new RestConfig
{
    AuthKey = "minioadmin",
    AuthSecret = "minioadmin",
    Endpoint = "http://localhost:9000",
    BucketName = "my-bucket",
    RootPath = "dev/cache"
};
var rest = new Rest("s3://", minioConfig, RestMode.S3);

// Store and cache operations (rootPath is automatically prefixed)
await rest.SetAsync("files/document.pdf", fileData); // Stored as "my-app/uploads/files/document.pdf"
var cacheProvider = rest.GetProvider<ICacheProvider>();
await cacheProvider.SetAsync("cache:key", data, TimeSpan.FromHours(2)); // Cached as "dev/cache/cache:key"
```

### 5. RabbitMQ Message Queuing

```csharp
// Add OElite.Restme.RabbitMQ package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "guest",        // RabbitMQ username
    AuthSecret = "guest"      // RabbitMQ password
};
var rest = new Rest("amqp://localhost", config, RestMode.RabbitMq);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("amqp://guest:guest@localhost", RestMode.RabbitMq);

// Publish message
await rest.QueuemeAsync("user.created", userData);

// Consume messages
await rest.DomeAsync<User>("user.created", async (user) => {
    // Process user
    return true; // Continue processing
});
```

### 6. ClickHouse Analytics

```csharp
// Add OElite.Restme.ClickHouse package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "default",        // ClickHouse username
    AuthSecret = "",            // ClickHouse password (empty for default)
    OperationMode = RestMode.ClickHouse
};
var rest = new Rest("clickhouse://localhost:8123", config);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("clickhouse://default:password@localhost:8123", new RestConfig
{
    OperationMode = RestMode.ClickHouse
});

// LINQ expressions automatically translate to ClickHouse SQL
var recentEvents = await rest.QueryAsync<UserEvent>(
    e => e.Timestamp > DateTime.UtcNow.AddHours(-1) &&
         e.EventType == "page_view");

// Time-series analytics
var hourlyStats = await rest.TimeSeriesAsync<UserEvent>(
    "user_events",
    DateTime.UtcNow.AddDays(-7),  // Start date
    DateTime.UtcNow,              // End date
    "hour"                        // Group by hour
);

// Aggregation queries
var userAnalytics = await rest.AggregateAsync(
    "user_events",
    "SELECT UserId, count() as EventCount, uniq(EventType) as UniqueEvents GROUP BY UserId");

// Raw SQL for complex queries
var results = await rest.QueryAsync<UserAnalytics>(
    "SELECT UserId, count() as TotalEvents FROM user_events WHERE Timestamp >= @start GROUP BY UserId",
    new { start = DateTime.UtcNow.AddDays(-30) }
);
```

### 7. Kafka Streaming

```csharp
// Add OElite.Restme.Kafka package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "kafka-user",        // SASL username (optional)
    AuthSecret = "kafka-password", // SASL password (optional)
    OperationMode = RestMode.Kafka
};
var rest = new Rest("kafka://localhost:9092", config);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("kafka://localhost:9092", new RestConfig
{
    OperationMode = RestMode.Kafka
});

// Publish messages
await rest.PublishAsync(new OrderEvent { OrderId = "123", Amount = 99.99m }, "orders", "user123");

// Batch publish
var events = GenerateOrderEvents(1000);
await rest.PublishAsync(events, "orders", order => order.UserId);

// Subscribe to topics
await rest.SubscribeAsync<OrderEvent>("orders", "order-processor",
    async (order) => {
        await ProcessOrderAsync(order);
    });

// Stream processing
var orderStream = await rest.ProcessAsync<OrderEvent>("orders", "realtime-analytics");
await foreach (var order in orderStream.Messages)
{
    await UpdateRealTimeMetricsAsync(order);
}
```

### 8. OpenSearch

```csharp
// Add OElite.Restme.OpenSearch package

// Option 1: Using RestConfig (recommended)
var config = new RestConfig
{
    AuthKey = "admin",        // OpenSearch username
    AuthSecret = "password",  // OpenSearch password
    OperationMode = RestMode.OpenSearch
};
var rest = new Rest("opensearch://localhost:9200", config);

// Option 2: Legacy connection string (still supported)
var rest = new Rest("opensearch://localhost:9200", new RestConfig
{
    OperationMode = RestMode.OpenSearch
});

// Index documents
await rest.IndexAsync(new Product { Id = "123", Name = "Laptop", Price = 1299.99m }, "products");

// Bulk index
await rest.IndexAsync(products, "products");

// Full-text search
var results = await rest.SearchAsync<Product>("high performance laptop",
    new[] { "name", "description" });

// Advanced search with queries
var expensiveProducts = await rest.SearchAsync<Product>(
    new SearchQuery {
        Query = "price:[1000 TO *] AND category:electronics",
        PageSize = 50
    }, "products");

// Get by ID
var product = await rest.GetAsync<Product>("123", "products");
```

### 9. Named Providers (NEW in v2.1.0)

**NEW Feature**: Support for multiple provider instances of the same type using named providers:

```csharp
using OElite;

// Create Rest instance
var rest = new Rest("redis://localhost:6379", new RestConfig { OperationMode = RestMode.Redis });

// Get named providers for different purposes
var userCache = rest.GetProvider<ICacheProvider>("users");
var sessionCache = rest.GetProvider<ICacheProvider>("sessions");
var apiCache = rest.GetProvider<ICacheProvider>("api-responses");
var tempCache = rest.GetProvider<ICacheProvider>("temporary");

// Use different cache providers for logical separation
await userCache.SetAsync("user:123", userData, TimeSpan.FromMinutes(30));
await sessionCache.SetAsync("session:abc123", sessionData, TimeSpan.FromHours(24));
await apiCache.SetAsync("api:external-service:users", apiResponse, TimeSpan.FromMinutes(10));
await tempCache.SetAsync("temp:processing:456", processingData, TimeSpan.FromMinutes(5));

// Default provider (backward compatible)
var defaultCache = rest.GetProvider<ICacheProvider>(); // Same as GetProvider<ICacheProvider>("default")

// Named providers work with all provider types
var orderStreaming = rest.GetProvider<IStreamingProvider>("orders");
var analyticsStreaming = rest.GetProvider<IStreamingProvider>("analytics");
var primaryStorage = rest.GetProvider<IStorageProvider>("primary");
var backupStorage = rest.GetProvider<IStorageProvider>("backup");

// Example: Multi-tier architecture with named providers
public class MultiTierDataService
{
    private readonly ICacheProvider _l1Cache;    // Fast cache
    private readonly ICacheProvider _l2Cache;    // Slower but larger cache
    private readonly IStorageProvider _hotStorage;   // Frequently accessed data
    private readonly IStorageProvider _coldStorage;  // Archival data

    public MultiTierDataService(Rest rest)
    {
        _l1Cache = rest.GetProvider<ICacheProvider>("l1-cache");
        _l2Cache = rest.GetProvider<ICacheProvider>("l2-cache");
        _hotStorage = rest.GetProvider<IStorageProvider>("hot-storage");
        _coldStorage = rest.GetProvider<IStorageProvider>("cold-storage");
    }

    public async Task<T> GetDataAsync<T>(string key) where T : class
    {
        // Try L1 cache first (fastest)
        var l1Data = await _l1Cache.GetAsync<T>(key);
        if (l1Data != null) return l1Data;

        // Try L2 cache
        var l2Data = await _l2Cache.GetAsync<T>(key);
        if (l2Data != null)
        {
            // Store in L1 for next time
            await _l1Cache.SetAsync(key, l2Data, TimeSpan.FromMinutes(5));
            return l2Data;
        }

        // Try hot storage
        var hotData = await _hotStorage.GetAsync<T>(key);
        if (hotData != null)
        {
            // Cache in both levels
            await _l1Cache.SetAsync(key, hotData, TimeSpan.FromMinutes(5));
            await _l2Cache.SetAsync(key, hotData, TimeSpan.FromHours(1));
            return hotData;
        }

        // Fallback to cold storage
        var coldData = await _coldStorage.GetAsync<T>(key);
        if (coldData != null)
        {
            // Cache in all levels
            await _l1Cache.SetAsync(key, coldData, TimeSpan.FromMinutes(5));
            await _l2Cache.SetAsync(key, coldData, TimeSpan.FromHours(1));
            await _hotStorage.SetAsync(key, coldData); // Promote to hot storage
        }

        return coldData;
    }
}
```

### 10. Base Providers (No additional packages required)

Use built-in base providers for simple scenarios without external infrastructure.

```csharp
using OElite;

// Memory cache
var restMemoryCache = new Rest(
    configuration: new RestConfig { OperationMode = RestMode.Memory }
);
await restMemoryCache.CachemeAsync("user:123", userData, TimeSpan.FromMinutes(30));

// Local file system storage (uses default AppContext.BaseDirectory/restme_storage)
var restLocalFs = new Rest(
    endPointOrConnectionString: "/var/data/myapp", // optional base directory; omit to use default
    configuration: new RestConfig { OperationMode = RestMode.LocalFileSystem }
);
await restLocalFs.SetAsync("docs/report.pdf", fileBytes);

// In-memory queue (single-process)
var restInMemoryQueue = new Rest(
    configuration: new RestConfig { OperationMode = RestMode.Memory }
);
await restMemoryCache.CachemeAsync("user:123", userData, TimeSpan.FromMinutes(30));
var cached = await restMemoryCache.FindmeAsync<User>("user:123");

// Local file system storage (uses default AppContext.BaseDirectory/restme_storage)
var restLocalFs = new Rest(
    endPointOrConnectionString: "/var/data/myapp", // optional base directory; omit to use default
    configuration: new RestConfig { OperationMode = RestMode.LocalFileSystemAsStorage }
);
await restLocalFs.SetAsync("docs/report.pdf", fileBytes);
var file = await restLocalFs.GetAsync<byte[]>("docs/report.pdf");

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
- `RestMode.HttpRest` - HTTP REST client (alternative)
- `RestMode.Redis` - Redis caching and queuing
- `RestMode.Azure` - Azure Blob Storage and caching
- `RestMode.S3` - S3-compatible storage and caching
- `RestMode.RabbitMq` - RabbitMQ message queuing
- `RestMode.ClickHouse` - ClickHouse columnar analytics
- `RestMode.Kafka` - Kafka streaming
- `RestMode.OpenSearch` - OpenSearch full-text search
- `RestMode.Memory` - In-memory cache and queuing (base provider)
- `RestMode.LocalFileSystem` - Local filesystem storage (base provider)

### Authentication Configuration

#### Using RestConfig (Recommended)
```csharp
var config = new RestConfig
{
    AuthKey = "username/account/id",        // Provider-specific username/account
    AuthSecret = "password/secret/key",     // Provider-specific password/secret
    Endpoint = "https://service.endpoint",  // Service endpoint (optional)
    Region = "us-east-1",                   // Region for cloud services (optional)
    BucketName = "my-bucket",               // Bucket/container name (optional)
    RootPath = "my-app/data"                // Logical path prefix (optional)
};
```

#### Provider-Specific Authentication

**Redis:**
```csharp
var config = new RestConfig { AuthSecret = "redis-password" };
var rest = new Rest("localhost:6379", config, RestMode.Redis);
```

**Azure Blob Storage:**
```csharp
var config = new RestConfig {
    AuthKey = "account-name",
    AuthSecret = "account-key",
    Endpoint = "core.windows.net"
};
var rest = new Rest("DefaultEndpointsProtocol=https", config, RestMode.Azure);
```

**S3-Compatible:**
```csharp
var config = new RestConfig {
    AuthKey = "access-key-id",
    AuthSecret = "secret-access-key",
    Endpoint = "https://s3.amazonaws.com",
    Region = "us-west-2",
    BucketName = "my-bucket"
};
var rest = new Rest("s3://", config, RestMode.S3);
```

**RabbitMQ:**
```csharp
var config = new RestConfig {
    AuthKey = "guest",
    AuthSecret = "guest"
};
var rest = new Rest("amqp://localhost", config, RestMode.RabbitMq);
```

**ClickHouse:**
```csharp
var config = new RestConfig {
    AuthKey = "default",
    AuthSecret = "password",
    OperationMode = RestMode.ClickHouse
};
var rest = new Rest("clickhouse://localhost:8123", config);
```

**Kafka:**
```csharp
var config = new RestConfig {
    AuthKey = "kafka-user",        // SASL username (optional)
    AuthSecret = "kafka-password", // SASL password (optional)
    OperationMode = RestMode.Kafka
};
var rest = new Rest("kafka://localhost:9092", config);
```

**OpenSearch:**
```csharp
var config = new RestConfig {
    AuthKey = "admin",
    AuthSecret = "password",
    OperationMode = RestMode.OpenSearch
};
var rest = new Rest("opensearch://localhost:9200", config);
```

#### Legacy Connection Strings (Still Supported)

**Redis:**
```
localhost:6379
redis://localhost:6379,password=your-password
```

**Azure Blob Storage:**
```
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net;RootPath=my-app/uploads
```

**S3-Compatible Providers:**
```
AccessKeyId=your_key;SecretAccessKey=your_secret;ServiceUrl=https://your-endpoint.com;BucketName=your-bucket;ForcePathStyle=true;RootPath=my-app/uploads
```

**RabbitMQ:**
```
amqp://localhost
amqp://user:password@localhost:5672
```

## 📚 Advanced Usage

### Generic Operations

The `Rest` class provides generic operations that work across all backends:

```csharp
var rest = new Rest(connectionString, RestMode.S3);

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
await rest.SetAsync("documents/report.pdf", fileStream);

// Retrieve as stream
var stream = await rest.GetAsync<Stream>("documents/report.pdf");
```

### Cache Operations

Advanced caching with expiry and CDN support:

```csharp
// Cache with custom expiry
var cacheProvider = rest.GetProvider<ICacheProvider>();
await cacheProvider.SetAsync("user:123", userData, TimeSpan.FromMinutes(30));

// Check if cached
var exists = await cacheProvider.ExistsAsync("user:123");

// Set expiry
await cacheProvider.SetExpiryAsync("user:123", TimeSpan.FromHours(1));

// Force expiry (using ExpiremeAsync extension)
await cacheProvider.ExpiremeAsync("user:123");

// Find with callback
var cacheProvider = rest.GetProvider<ICacheProvider>();
var user = await cacheProvider.FindmeAsync<User>("user:123", additionalValidation: async (u) => {
    // Process user data
    return true;
});
```

## Cache Extension Methods

Restme provides powerful extension methods for advanced cache operations with direct data storage and expiry management:

### CachemeAsync - Store with Expiry
```csharp
var cacheProvider = rest.GetProvider<ICacheProvider>();

// Store with default expiry (60 seconds)
var success = await cacheProvider.CachemeAsync("user:123", userData);

// Store with custom expiry (1 hour)
var success = await cacheProvider.CachemeAsync("user:123", userData, expiryInSeconds: 3600);

// Store using query object as key (MD5 hash generated automatically)
var query = new { UserId = 123, IncludeProfile = true };
var success = await cacheProvider.CachemeAsync(query, userData, expiryInSeconds: 1800);
```

### FindmeAsync - Retrieve with Validation
```csharp
// Simple retrieval
var user = await cacheProvider.FindmeAsync<User>("user:123");

// With additional validation
var user = await cacheProvider.FindmeAsync<User>("user:123",
    additionalValidation: async (u) => u.IsActive && u.LastLoginDate > DateTime.UtcNow.AddDays(-30));

// With refresh action for cache misses
var user = await cacheProvider.FindmeAsync<User>("user:123",
    refreshAction: async () => await userService.GetUserFromDatabaseAsync(123));

// Using query object as key
var query = new { UserId = 123, IncludeProfile = true };
var user = await cacheProvider.FindmeAsync<User>(query,
    refreshAction: async () => await userService.GetUserWithProfileAsync(123));
```

### ExpiremeAsync - Force Expiry
```csharp
// Force expiry by removing from cache
await cacheProvider.ExpiremeAsync("user:123");

// Force expiry using query object
var query = new { UserId = 123 };
await cacheProvider.ExpiremeAsync<User>(query);
```

### Direct Data Storage Benefits

The updated extension methods provide:

- **✅ Zero Data Tampering**: User data stored exactly as provided (no ResponseMessage wrapper)
- **✅ Provider-Optimized Expiry**: Each provider uses native expiry mechanisms
- **✅ Consistent API**: Same interface across Memory, Redis, and S3 providers
- **✅ Automatic Key Generation**: MD5 hashing for complex query objects
- **✅ Type Safety**: Strongly-typed generic methods
- **✅ Background Refresh**: Non-blocking cache refresh for expired items

### Expiry Validation Per Provider

| Provider | Expiry Mechanism | Cleanup Method |
|----------|------------------|----------------|
| **MemoryCache** | In-memory tuple with timer | Automatic timer-based cleanup |
| **RedisCache** | Native Redis TTL | Redis-managed expiry |
| **S3Cache** | Object metadata validation | Background async removal |

### Expiry & Lifecycle Management

Restme provides comprehensive expiry configuration across all supported platforms:

#### ClickHouse TTL (Time To Live)
```csharp
// Set TTL on table data
await rest.SetTableTTLAsync("events", "created_at + INTERVAL 30 DAY");

// Create TTL index for automatic cleanup
await rest.CreateTTLIndexAsync("logs", "timestamp", TimeSpan.FromDays(90));
```

#### Kafka Topic Retention
```csharp
// Configure topic retention policy
await rest.SetTopicRetentionAsync("user-events", TimeSpan.FromDays(7));

// Set message expiry based on timestamp
await rest.SetMessageExpiryAsync("temp-data", TimeSpan.FromHours(24));
```

#### OpenSearch Index Lifecycle
```csharp
// Set TTL on documents
await rest.SetIndexTTLAsync("logs-*", "timestamp", TimeSpan.FromDays(30));

// Configure automatic index deletion
await rest.SetIndexLifecyclePolicyAsync("temp-data", TimeSpan.FromDays(90));
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
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;RootPath=my-app/uploads", RestMode.S3);

// Operations automatically use the root path
await rest.SetAsync("documents/file.pdf", data); // Stored as "my-app/uploads/documents/file.pdf"
var cacheProvider = rest.GetProvider<ICacheProvider>();
await cacheProvider.SetAsync("user:123", userData); // Cached as "my-app/uploads/user:123"

// Root path is applied to all operations (GET, PUT, DELETE, EXISTS)
var file = await rest.GetAsync<byte[]>("documents/file.pdf"); // Retrieves from "my-app/uploads/documents/file.pdf"
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

Advanced MongoDB library with comprehensive query capabilities, high-performance updates, aggregation pipelines, and denormalization system.

**Key Features:**
- **High-Performance UpdateBuilder**: 2-3x faster updates with fluent API and BSON optimization
- **Enhanced LINQ Extensions**: Complete LINQ support with advanced aggregation operations
- **Type-safe MongoDB operations** with full IntelliSense support
- **Advanced aggregation pipelines** for complex queries with expression-to-pipeline conversion
- **Automatic denormalization** for efficient data relationships
- **Enhanced LINQ expression support** with nested documents and extension methods
- **Performance-optimized aggregation methods** using MongoDB's native capabilities
- **MongoDB class mapping** with conflict resolution
- ⭐ **MongoDB-Free Application Layer**: Complete abstraction eliminates MongoDB dependencies from application code
- **Zero Vendor Lock-in**: Application developers work with pure .NET types and collections
- **Clean Architecture**: Perfect separation between business logic and database implementation
- 🆕 **MongoDbDocument API**: Replace BsonDocument with pure .NET Dictionary-based operations
- 🆕 **IMongoDbCollection Interface**: MongoDB-free collection operations with Dictionary and lambda support
- 🆕 **Seamless Type Conversion**: Internal MongoDB type conversion while exposing clean .NET APIs

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

// Repository with enhanced LINQ support and high-performance updates
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> ProductStock => new(_adapter.GetCollection<Product>());

    // Enhanced LINQ queries with extension method support
    public async Task<List<Product>> GetExpensiveProductsAsync(decimal minPrice)
    {
        return await ProductStock
            .Where(p => p.Price > minPrice)
            .Where(p => p.Name.IsNotNullOrEmpty()) // Extension method support
            .OrderByDescending(p => p.Price)
            .Take(10)
            .ToListAsync();
    }

    // High-performance updates with fluent API
    public async Task UpdateProductAsync(DbObjectId productId, string newName, decimal newPrice)
    {
        await ProductStock
            .Where(p => p.Id == productId)
            .UpdateAsync(u => u
                .Set(p => p.Name, newName)
                .Set(p => p.Price, newPrice)
                .Inc(p => p.ViewCount, 1)
                .CurrentDate(p => p.UpdatedAt));
    }

    // Advanced aggregation operations
    public async Task<List<ProductSummary>> GetProductSummariesAsync()
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .SelectAsync(p => new ProductSummary
            {
                Name = p.Name,
                Price = p.Price,
                CategoryName = p.Category.Name // Nested property support
            });
    }

    // Performance-optimized collection fetching
    public async Task<ProductCollection> GetActiveProductsAsync(int pageIndex, int pageSize)
    {
        return await ProductStock
            .Where(p => p.Status == EntityStatus.Active)
            .OrderBy(p => p.Name)
            .FetchAsync<Product, ProductCollection>(pageIndex, pageSize, returnTotalCount: false);
    }

    // ⭐ MongoDB-Free Aggregation: Zero MongoDB dependencies in application code
    public async Task<List<(string Query, int Count)>> GetPopularSearchTermsAsync(DbObjectId merchantId)
    {
        // Complex aggregation pipeline using pure Dictionary<string, object>
        var pipeline = new Dictionary<string, object>[]
        {
            new Dictionary<string, object> {
                ["$match"] = new Dictionary<string, object> {
                    ["ownerMerchantId"] = merchantId.ToString(),
                    ["searchedOnUtc"] = new Dictionary<string, object> { ["$gte"] = DateTime.UtcNow.AddDays(-7) }
                }
            },
            new Dictionary<string, object> {
                ["$group"] = new Dictionary<string, object> {
                    ["_id"] = "$normalizedQuery",
                    ["count"] = new Dictionary<string, object> { ["$sum"] = 1 }
                }
            },
            new Dictionary<string, object> {
                ["$sort"] = new Dictionary<string, object> { ["count"] = -1 }
            },
            new Dictionary<string, object> {
                ["$limit"] = 10
            }
        };

        // Returns List<Dictionary<string, object>> - no MongoDB types exposed
        var results = await DbCentre.SearchHistory.AggregateAsync<Dictionary<string, object>>(pipeline);

        // Application code uses standard .NET types
        return results.Select(doc => (
            Query: (string)doc["_id"],
            Count: (int)doc["count"]
        )).ToList();
    }

    // 🆕 MongoDB-Free Collection Operations: New API for zero-dependency database operations
    public async Task<List<Product>> GetProductsWithMongoDbFreeAPI(string categoryName)
    {
        // Get MongoDB-free collection interface - no MongoDB types exposed
        var productsCollection = DbCentre.GetMongoDbCollection<Product>();

        // Use strongly-typed operations with lambda expressions
        var activeProducts = await productsCollection.FindAsync(p => p.Status == EntityStatus.Active);

        // Or use Dictionary-based filters for dynamic queries
        var categoryFilter = new Dictionary<string, object>
        {
            ["categoryName"] = categoryName,
            ["status"] = (int)EntityStatus.Active
        };

        var categoryProducts = await productsCollection.FindAsync(categoryFilter);

        return categoryProducts;
    }

    // 🆕 MongoDbDocument API: Direct document operations without MongoDB dependencies
    public async Task<List<MongoDbDocument>> GetProductDocuments(string searchTerm)
    {
        // Get raw MongoDB-free collection for document operations
        var collection = DbCentre.GetMongoDbCollection("products");

        // Create filter using MongoDbDocument (replaces BsonDocument)
        var filter = new MongoDbDocument
        {
            ["name"] = new MongoDbDocument { ["$regex"] = searchTerm, ["$options"] = "i" },
            ["status"] = 1
        };

        // Returns List<MongoDbDocument> - pure .NET types, no MongoDB dependencies
        var results = await collection.FindAsync(filter);

        return results;
    }
}
```

#### 🏗️ Clean Architecture: Zero MongoDB Dependencies

OElite.Restme.MongoDb provides complete abstraction from MongoDB specifics, ensuring your application code remains vendor-neutral and testable.

**Before (Traditional MongoDB.Driver usage):**
```csharp
using MongoDB.Driver;  // ❌ Direct MongoDB dependency
using MongoDB.Bson;    // ❌ Exposes internal types

public async Task<List<BsonDocument>> GetReports()  // ❌ MongoDB types in return signature
{
    var collection = _database.GetCollection<BsonDocument>("reports");
    var pipeline = new BsonDocument[]  // ❌ MongoDB-specific pipeline format
    {
        new BsonDocument("$match", new BsonDocument("status", "active")),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$category" },
            { "count", new BsonDocument("$sum", 1) }
        })
    };

    var cursor = await collection.AggregateAsync(pipeline);
    return await cursor.ToListAsync();  // ❌ Returns MongoDB-specific types
}
```

**After (OElite.Restme.MongoDb with MongoDB-Free API):**
```csharp
// ✅ Zero MongoDB dependencies - clean application code

public async Task<List<(string Category, int Count)>> GetReports()  // ✅ Pure .NET return types
{
    // Get MongoDB-free collection interface
    var reportsCollection = DbCentre.GetMongoDbCollection("reports");

    var pipeline = new Dictionary<string, object>[]  // ✅ Standard .NET collections
    {
        new Dictionary<string, object> {
            ["$match"] = new Dictionary<string, object> { ["status"] = "active" }
        },
        new Dictionary<string, object> {
            ["$group"] = new Dictionary<string, object> {
                ["_id"] = "$category",
                ["count"] = new Dictionary<string, object> { ["$sum"] = 1 }
            }
        }
    };

    // ✅ Returns List<Dictionary<string, object>> - no MongoDB types
    var results = await reportsCollection.AggregateAsync(pipeline);

    // ✅ Application code works with standard .NET types
    return results.Select(doc => (
        Category: (string)doc["_id"],
        Count: (int)doc["count"]
    )).ToList();
}

// 🆕 NEW: MongoDbDocument API (replaces BsonDocument)
public async Task<List<MongoDbDocument>> GetActiveReportsAsync()
{
    var reportsCollection = DbCentre.GetMongoDbCollection("reports");

    // Create filter using MongoDbDocument instead of BsonDocument
    var filter = new MongoDbDocument
    {
        ["status"] = "active",
        ["createdAt"] = new MongoDbDocument { ["$gte"] = DateTime.UtcNow.AddDays(-30) }
    };

    // ✅ Returns List<MongoDbDocument> - pure .NET types
    return await reportsCollection.FindAsync(filter);
}

// 🆕 NEW: Strongly-typed MongoDB-free operations
public async Task<List<Report>> GetReportsWithTypedAPI()
{
    var reportsCollection = DbCentre.GetMongoDbCollection<Report>();

    // Lambda expressions - no MongoDB types needed
    var reports = await reportsCollection.FindAsync(r => r.Status == "active");

    // Dictionary filters for dynamic queries
    var dynamicFilter = new Dictionary<string, object>
    {
        ["status"] = "active",
        ["priority"] = new Dictionary<string, object> { ["$in"] = new[] { "high", "urgent" } }
    };

    var priorityReports = await reportsCollection.FindAsync(dynamicFilter);

    return reports;
}
```

**Architecture Benefits:**
- 🎯 **Zero Vendor Lock-in**: Switch databases without changing application logic
- 🧪 **Enhanced Testability**: Mock with standard .NET interfaces and collections
- 📚 **Clean Domain Models**: Business logic free from infrastructure concerns
- 🔄 **Future-Proof**: Database implementation changes don't affect application code
- 👥 **Developer Experience**: Team members don't need MongoDB expertise for application development

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

### Scenario 2: Enterprise API with Rate Limiting and High-Performance MongoDB
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

// Repository with enhanced MongoDB operations
public class ProductRepository : DataRepository
{
    public MongoQuery<Product> Products => new(_adapter.GetCollection<Product>());

    // High-performance LINQ queries with aggregation
    public async Task<ProductCollection> GetPopularProductsAsync(int pageIndex, int pageSize)
    {
        return await Products
            .Where(p => p.Status == EntityStatus.Active)
            .Where(p => p.Rating > 4.0)
            .OrderByDescending(p => p.SalesCount)
            .FetchAsync<Product, ProductCollection>(pageIndex, pageSize, returnTotalCount: true);
    }

    // Advanced aggregation operations
    public async Task<List<CategorySummary>> GetCategoryStatisticsAsync()
    {
        return await Products
            .Where(p => p.Status == EntityStatus.Active)
            .SelectAsync(p => new CategorySummary
            {
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                TotalProducts = 1,
                AveragePrice = p.Price
            });
    }

    // High-performance batch updates
    public async Task UpdateProductPricesAsync(decimal categoryId, decimal priceMultiplier)
    {
        await Products
            .Where(p => p.CategoryId == categoryId)
            .UpdateAsync(u => u
                .Mul(p => p.Price, priceMultiplier)
                .Inc(p => p.UpdateCount, 1)
                .CurrentDate(p => p.UpdatedAt));
    }

    // Server-side mathematical operations
    public async Task<decimal> GetCategoryTotalValueAsync(DbObjectId categoryId)
    {
        return await Products
            .Where(p => p.CategoryId == categoryId)
            .Where(p => p.Status == EntityStatus.Active)
            .SumAsync(p => p.Price);
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
        _storage = new Rest("AccessKeyId=...;ServiceUrl=https://s3.amazonaws.com", RestMode.S3);
        _cache = new Rest("localhost:6379", RestMode.Redis);
        _queue = new Rest("amqp://localhost", RestMode.RabbitMq);
    }

    public async Task<byte[]> GetDocumentAsync(string documentId)
    {
        // Try cache first
        var cached = await _cache.FindmeAsync<byte[]>($"doc:{documentId}");
        if (cached != null) return cached;

        // Fetch from storage
        var document = await _storage.GetAsync<byte[]>($"documents/{documentId}");

        // Cache for future requests
        await _cache.CachemeAsync($"doc:{documentId}", document, TimeSpan.FromHours(1));

        return document;
    }

    public async Task SaveDocumentAsync(string documentId, byte[] data)
    {
        // Save to storage
        await _storage.SetAsync($"documents/{documentId}", data);

        // Invalidate cache
        await _cache.RemoveAsync($"doc:{documentId}");

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