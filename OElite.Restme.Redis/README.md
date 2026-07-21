# OElite.Restme.Redis

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.Redis.svg)](https://www.nuget.org/packages/OElite.Restme.Redis)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Redis integration package for the Restme framework, providing high-performance caching and distributed data storage capabilities with enterprise-grade features.

## Overview

OElite.Restme.Redis provides a robust Redis integration for the OElite platform, offering high-performance caching, distributed storage, and session management. Built on StackExchange.Redis, it delivers enterprise-grade reliability with automatic failover, connection pooling, and advanced caching strategies.

## Features

- **Generic Provider Factory**: Auto-registered via `ServiceLocator` with capability detection
- **Provider Capability**: `ProviderCapabilities.Cache` - Redis exclusively provides caching
- **High-Performance Caching**: Lightning-fast Redis-based caching with connection multiplexing
- **Automatic Serialization**: JSON serialization/deserialization with support for complex objects
- **Connection Management**: Robust connection handling with automatic retry and failover
- **Type Safety**: Strongly-typed cache operations with generic support
- **Expiration Management**: Flexible TTL (Time-To-Live) support for cache entries
- **Error Handling**: Comprehensive error handling with custom exceptions
- **Async/Await Support**: Full asynchronous operations for optimal performance
- **Key Validation**: Built-in key validation and sanitization

## Installation

```bash
dotnet add package OElite.Restme.Redis
```

## Quick Start

### Provider Factory Auto-Registration

OElite.Restme.Redis automatically registers itself with the service locator when loaded:

```csharp
// Automatic registration happens when assembly is loaded
// The RedisServiceFactory registers itself as "redis" provider

// Check provider capabilities
var factory = ServiceLocator.GetFactory("redis");
var capabilities = factory?.SupportedCapabilities;
// capabilities == ProviderCapabilities.Cache

// Check if cache provider can be created
bool canCreateCache = factory?.CanCreateProvider<ICacheProvider>() ?? false;
// canCreateCache == true

bool canCreateStorage = factory?.CanCreateProvider<IStorageProvider>() ?? false;
// canCreateStorage == false (Redis only supports caching)
```

### Basic Configuration

```csharp
using OElite;
using OElite.Abstractions;

// Option 1: Using Rest with generic provider pattern (recommended)
var rest = new Rest("redis://localhost:6379",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Redis,
        AuthSecret = "your-password" // Optional
    });

// Get cache provider using generic factory pattern
var cacheProvider = rest.GetProvider<ICacheProvider>();

// NEW: Named providers for multiple cache instances
var primaryCache = rest.GetProvider<ICacheProvider>("primary");
var sessionCache = rest.GetProvider<ICacheProvider>("sessions");
var tempCache = rest.GetProvider<ICacheProvider>("temporary");

// Option 2: Direct provider instantiation (still supported)
var config = new RestConfig { AuthSecret = "your-redis-password" };
var directProvider = new RedisCacheProvider("localhost:6379", config);
```

### Basic Cache Operations

```csharp
// Store data in cache
await cacheProvider.SetAsync("user:123", userData, TimeSpan.FromHours(1));

// Retrieve data from cache
var user = await cacheProvider.GetAsync<User>("user:123");

// Check if key exists
bool exists = await cacheProvider.ExistsAsync("user:123");

// Remove from cache
await cacheProvider.RemoveAsync("user:123");
```

## Core Features

### Type-Safe Caching

Store and retrieve strongly-typed objects:

```csharp
// Store complex objects
public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}

var product = new Product
{
    Id = "prod-123",
    Name = "Sample Product",
    Price = 99.99m,
    CreatedAt = DateTime.UtcNow
};

// Cache with expiration
await cacheProvider.SetAsync($"product:{product.Id}", product, TimeSpan.FromMinutes(30));

// Retrieve typed object
var cachedProduct = await cacheProvider.GetAsync<Product>($"product:{product.Id}");
```

### String Caching

Direct string storage for simple scenarios:

```csharp
// Store strings directly
await cacheProvider.SetAsync("session:token", authToken, TimeSpan.FromHours(2));

// Retrieve strings
var token = await cacheProvider.GetAsync<string>("session:token");
```

### Expiration Management

Multiple expiration strategies:

```csharp
// Set with specific TTL
await cacheProvider.SetAsync("temp:data", tempData, TimeSpan.FromMinutes(5));

// Set without expiration (persistent until manually removed)
await cacheProvider.SetAsync("config:settings", settings);

// Set with absolute expiration
var absoluteExpiry = DateTime.UtcNow.AddDays(1);
await cacheProvider.SetAsync("daily:report", report, absoluteExpiry - DateTime.UtcNow);
```

## Advanced Usage

### Bulk Operations

```csharp
// Store multiple items efficiently
var tasks = new List<Task>();
foreach (var item in items)
{
    tasks.Add(cacheProvider.SetAsync($"bulk:{item.Id}", item, TimeSpan.FromHours(1)));
}
await Task.WhenAll(tasks);

// Retrieve multiple items
var retrieveTasks = ids.Select(id => cacheProvider.GetAsync<Item>($"bulk:{id}"));
var results = await Task.WhenAll(retrieveTasks);
```

### Conditional Operations

```csharp
// Only set if key doesn't exist
if (!await cacheProvider.ExistsAsync("unique:key"))
{
    await cacheProvider.SetAsync("unique:key", uniqueData);
}

// Cache-aside pattern
public async Task<User> GetUserAsync(string userId)
{
    var cacheKey = $"user:{userId}";

    // Try cache first
    var cachedUser = await cacheProvider.GetAsync<User>(cacheKey);
    if (cachedUser != null)
        return cachedUser;

    // Fallback to database
    var user = await _userRepository.GetByIdAsync(userId);
    if (user != null)
    {
        // Store in cache for future requests
        await cacheProvider.SetAsync(cacheKey, user, TimeSpan.FromMinutes(15));
    }

    return user;
}
```

### Named Cache Providers

**NEW in v2.1.0**: Support for multiple cache provider instances using named providers:

```csharp
// Create Rest instance
var rest = new Rest("redis://localhost:6379", new RestConfig { OperationMode = RestMode.Redis });

// Get named cache providers for different use cases
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

// Named providers enable organized caching strategies
public class UserService
{
    private readonly ICacheProvider _userProfileCache;
    private readonly ICacheProvider _userPermissionsCache;
    private readonly ICacheProvider _userPreferencesCache;

    public UserService(Rest rest)
    {
        _userProfileCache = rest.GetProvider<ICacheProvider>("user-profiles");
        _userPermissionsCache = rest.GetProvider<ICacheProvider>("user-permissions");
        _userPreferencesCache = rest.GetProvider<ICacheProvider>("user-preferences");
    }

    public async Task<UserProfile> GetUserProfileAsync(string userId)
    {
        var cacheKey = $"profile:{userId}";

        // Check cache first
        var cached = await _userProfileCache.GetAsync<UserProfile>(cacheKey);
        if (cached != null) return cached;

        // Fetch from database and cache
        var profile = await _userRepository.GetProfileAsync(userId);
        if (profile != null)
        {
            await _userProfileCache.SetAsync(cacheKey, profile, TimeSpan.FromMinutes(15));
        }

        return profile;
    }

    public async Task<List<Permission>> GetUserPermissionsAsync(string userId)
    {
        var cacheKey = $"permissions:{userId}";

        // Use dedicated permissions cache with longer TTL
        var cached = await _userPermissionsCache.GetAsync<List<Permission>>(cacheKey);
        if (cached != null) return cached;

        var permissions = await _permissionService.GetUserPermissionsAsync(userId);
        await _userPermissionsCache.SetAsync(cacheKey, permissions, TimeSpan.FromHours(1));

        return permissions;
    }
}
```

### Error Handling

```csharp
try
{
    var data = await cacheProvider.GetAsync<MyData>("my:key");
}
catch (OEliteException ex)
{
    // Handle cache-specific errors
    _logger.LogError(ex, "Cache operation failed");
    // Fallback to alternative data source
}
catch (Exception ex)
{
    // Handle general errors
    _logger.LogError(ex, "Unexpected error during cache operation");
}
```

## Integration Patterns

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<ICacheProvider>(provider =>
{
    var connectionString = configuration.GetConnectionString("Redis");
    var config = new RestConfig();
    return new RedisCacheProvider(connectionString, config);
});

// In your service
public class ProductService
{
    private readonly ICacheProvider _cache;
    private readonly IProductRepository _repository;

    public ProductService(ICacheProvider cache, IProductRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<Product> GetProductAsync(string id)
    {
        var cacheKey = $"product:{id}";

        var cached = await _cache.GetAsync<Product>(cacheKey);
        if (cached != null) return cached;

        var product = await _repository.GetByIdAsync(id);
        if (product != null)
        {
            await _cache.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10));
        }

        return product;
    }
}
```

### Session Management

```csharp
public class SessionManager
{
    private readonly ICacheProvider _cache;

    public SessionManager(ICacheProvider cache)
    {
        _cache = cache;
    }

    public async Task<UserSession> GetSessionAsync(string sessionId)
    {
        return await _cache.GetAsync<UserSession>($"session:{sessionId}");
    }

    public async Task SetSessionAsync(string sessionId, UserSession session)
    {
        await _cache.SetAsync($"session:{sessionId}", session, TimeSpan.FromHours(24));
    }

    public async Task InvalidateSessionAsync(string sessionId)
    {
        await _cache.RemoveAsync($"session:{sessionId}");
    }
}
```

## Configuration

### Authentication Configuration

#### Using RestConfig (Recommended)
```csharp
var config = new RestConfig
{
    AuthSecret = "your-redis-password" // Redis password
};
var cacheProvider = new RedisCacheProvider("localhost:6379", config);
```

#### Legacy Connection String Options (Still Supported)
```csharp
// Basic connection
"localhost:6379"

// With authentication
"localhost:6379,password=your-password"

// Multiple endpoints with failover
"server1:6379,server2:6379,server3:6379"

// With SSL
"your-redis-instance.com:6380,ssl=true,password=your-password"

// Azure Redis Cache
"your-cache.redis.cache.windows.net:6380,password=your-access-key,ssl=true"
```

### RestConfig Configuration

```csharp
var config = new RestConfig
{
    // Configure serialization, logging, and other options
    // Refer to OElite.Restme documentation for details
};
```

## Performance Considerations

### Connection Multiplexing

The Redis provider uses StackExchange.Redis connection multiplexing for optimal performance:

```csharp
// Single connection is reused across all operations
var provider = new RedisCacheProvider(connectionString, config);

// All operations share the same connection pool
await provider.SetAsync("key1", data1);
await provider.SetAsync("key2", data2);
await provider.GetAsync<Data>("key1");
```

### Serialization Performance

- **Strings**: Direct storage without serialization overhead
- **Objects**: JSON serialization using Newtonsoft.Json
- **Large Objects**: Consider compression for objects > 1MB

### Key Naming Strategies

Use consistent, hierarchical key naming:

```csharp
// Good: Hierarchical and descriptive
"user:profile:123"
"product:details:456"
"session:auth:abc-def"

// Avoid: Generic or unclear
"data123"
"temp"
"cache_item"
```

## Integration with OElite Platform

### With OElite.Restme.Hosting

```csharp
// Register with ASP.NET Core DI
services.AddOEliteRedisCache(configuration.GetConnectionString("Redis"));

// Use IDistributedCache interface
services.AddSingleton<IDistributedCache, RedisDistributedCache>();
```

### With OElite.Common

```csharp
// Leverage shared types and utilities
var cacheKey = CacheKeyBuilder.Build("user", userId);
await cacheProvider.SetAsync(cacheKey, user, CachePolicy.Standard);
```

## Error Handling

Common exceptions and handling strategies:

```csharp
try
{
    await cacheProvider.GetAsync<Data>("key");
}
catch (OEliteException ex) when (ex.Message.Contains("connection"))
{
    // Connection issues - implement fallback
    _logger.LogWarning("Redis connection failed, falling back to database");
}
catch (OEliteException ex) when (ex.Message.Contains("serialization"))
{
    // Serialization issues - data corruption or type mismatch
    _logger.LogError("Cache data serialization failed: {Error}", ex.Message);
    await cacheProvider.RemoveAsync("key"); // Remove corrupted data
}
```

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **StackExchange.Redis 2.9.17+**
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

RedisCacheProvider is thread-safe and designed for concurrent operations:

- Uses connection multiplexing for thread safety
- All async operations are thread-safe
- Can be used as a singleton in DI containers

## License

Copyright © Phanes Technology Ltd. All rights reserved.