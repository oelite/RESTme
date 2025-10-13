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
- `