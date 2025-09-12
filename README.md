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
var rest = new Rest("https://api.example.com", RestMode.HTTPClient);

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
var connectionString = "DefaultEndpointsProtocol=https;AccountName=...";
var rest = new Rest(connectionString, RestMode.AzureStorageClient);

// Store data
await rest.StoremAsync("documents/report.pdf", fileData);

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

// Backblaze B2
var rest = new Rest("AccessKeyId=...;SecretAccessKey=...;ServiceUrl=https://s3.us-west-004.backblazeb2.com;ForcePathStyle=true", RestMode.S3Client);

// MinIO (local development)
var rest = new Rest("AccessKeyId=minioadmin;SecretAccessKey=minioadmin;ServiceUrl=http://localhost:9000;ForcePathStyle=true;UseHttp=true", RestMode.S3Client);

// Store and cache operations
await rest.StoremAsync("files/document.pdf", fileData);
await rest.CachemeAsync("cache:key", data, TimeSpan.FromHours(2));
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

## 🔧 Configuration

### RestMode Options

- `RestMode.HTTPClient` - HTTP REST client
- `RestMode.RedisCacheClient` - Redis caching
- `RestMode.AzureStorageClient` - Azure Blob Storage
- `RestMode.S3Client` - S3-compatible storage
- `RestMode.RabbitMq` - RabbitMQ message queuing

### Connection Strings

#### Redis
```
localhost:6379
redis://localhost:6379
redis://user:password@localhost:6379
```

#### Azure Blob Storage
```
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net
```

#### S3-Compatible Providers
```
AccessKeyId=your_key;SecretAccessKey=your_secret;ServiceUrl=https://your-endpoint.com;BucketName=your-bucket;ForcePathStyle=true
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

OElite.Restme.S3 supports numerous S3-compatible providers:

### Supported Providers

- **Amazon S3** - Default AWS S3
- **Backblaze B2** - Cost-effective cloud storage
- **MinIO** - Self-hosted object storage
- **DigitalOcean Spaces** - Simple object storage
- **Cloudflare R2** - Cloudflare's object storage
- **Wasabi** - Hot cloud storage
- **Scaleway Object Storage** - European cloud storage
- **Any S3-compatible provider**

### Provider Examples

```csharp
// Backblaze B2
var rest = new Rest("AccessKeyId=key;SecretAccessKey=secret;ServiceUrl=https://s3.us-west-004.backblazeb2.com;ForcePathStyle=true", RestMode.S3Client);

// MinIO (local development)
var rest = new Rest("AccessKeyId=minioadmin;SecretAccessKey=minioadmin;ServiceUrl=http://localhost:9000;ForcePathStyle=true;UseHttp=true", RestMode.S3Client);

// DigitalOcean Spaces
var rest = new Rest("AccessKeyId=key;SecretAccessKey=secret;ServiceUrl=https://nyc3.digitaloceanspaces.com;ForcePathStyle=true", RestMode.S3Client);
```

## 🔒 Security Best Practices

1. **Use HTTPS**: Always use secure connections in production
2. **Rotate Keys**: Regularly rotate your access keys
3. **Bucket Policies**: Configure appropriate bucket policies
4. **VPC**: Use VPC endpoints when available
5. **IAM**: Use IAM roles when possible instead of access keys

## 🚀 Performance Tips

1. **Connection Pooling**: The AWS SDK handles connection pooling automatically
2. **CDN Integration**: Both cache and storage providers set appropriate cache headers
3. **Compression**: Consider compressing large objects before storage
4. **Batch Operations**: Use batch operations when possible
5. **Async Operations**: Always use async methods for better performance

## 🐛 Troubleshooting

### Common Issues

1. **403 Forbidden**: Check your credentials and permissions
2. **404 Not Found**: Ensure the resource exists and you have access
3. **Connection Timeout**: Verify the endpoint is correct and accessible
4. **SSL Errors**: Check if you need `UseHttp=true` for local development

### Debug Mode

Enable debug logging to see detailed request/response information:

```csharp
var rest = new Rest(connectionString, RestMode.S3Client);
rest.Logger = logger; // Your ILogger instance
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

Released under [MIT License](http://choosealicense.com/licenses/mit).

## 🔗 Links

- [NuGet Package](https://www.nuget.org/packages/OElite.Restme/)
- [GitHub Repository](https://github.com/your-org/OElite.Restme)
- [Documentation](https://github.com/your-org/OElite.Restme/wiki)