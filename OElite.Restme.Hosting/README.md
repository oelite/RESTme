# OElite.Restme.Hosting

ASP.NET Core integration extensions for **OElite.Restme** - provides dependency injection, IDistributedCache adapters, and middleware for seamless integration with Hosting/ASP.NET Core applications.

## Purpose

This package decouples Hosting/ASP.NET Core-specific extensions from the core Restme packages, keeping the core libraries framework-agnostic while providing first-class Hositng/ASP.NET Core support.

## Features

- ✅ **IDistributedCache adapter** for OElite.Restme.Redis
- ✅ **IMemoryCache adapter** for OElite.Restme memory provider
- ✅ **Memory distributed cache** for in-memory IDistributedCache implementations
- ✅ **Dependency injection extensions** for all Restme providers
- ✅ **Clean separation** between core abstractions and framework integrations
- ✅ **Multi-framework support** (net8.0, net9.0, net10.0)

## Installation

```bash
dotnet add package OElite.Restme.Hosting
```

## Usage

### Redis Distributed Cache

Replace `Microsoft.Extensions.Caching.StackExchangeRedis` with OElite's Redis provider:

```csharp
using OElite.Restme.Hosting.Redis;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Option 1: Simple configuration
        builder.Services.AddRestmeRedisCache(
            connectionString: "localhost:6379",
            instanceName: "myapp:"
        );

        // Option 2: Configuration-based setup
        builder.Services.AddRestmeRedisCache(options =>
        {
            options.ConnectionString = builder.Configuration["oelite:data:redis:platform"];
            options.InstanceName = "myapp:";
        });

        var app = builder.Build();
        app.Run();
    }
}
```

### Memory Cache

Replace `Microsoft.Extensions.Caching.Memory` with OElite's memory provider:

```csharp
using OElite.Restme.Hosting.Memory;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Option 1: IMemoryCache only
        builder.Services.AddRestmeMemoryCache("myapp:");

        // Option 2: Both IMemoryCache and IDistributedCache (in-memory)
        builder.Services.AddRestmeMemoryCacheWithDistributed("myapp:");

        // Option 3: Configuration-based setup
        builder.Services.AddRestmeMemoryCache(options =>
        {
            options.InstanceName = "myapp:";
        });

        var app = builder.Build();
        app.Run();
    }
}
```

### Using IDistributedCache

Once registered, `IDistributedCache` is available for dependency injection:

```csharp
public class MyService
{
    private readonly IDistributedCache _cache;

    public MyService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> GetCachedValueAsync(string key)
    {
        var bytes = await _cache.GetAsync(key);
        if (bytes == null) return null;

        return Encoding.UTF8.GetString(bytes);
    }

    public async Task SetCachedValueAsync(string key, string value, TimeSpan expiry)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };

        await _cache.SetAsync(key, bytes, options);
    }
}
```

### Using IMemoryCache

Once registered, `IMemoryCache` is available for dependency injection:

```csharp
public class MyService
{
    private readonly IMemoryCache _cache;

    public MyService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string? GetCachedValue(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    public void SetCachedValue(string key, string value, TimeSpan expiry)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };

        _cache.Set(key, value, options);
    }
}
```

### Using ICacheProvider (OElite Native)

You can also use the OElite-native `ICacheProvider` interface for more control:

```csharp
public class MyService
{
    private readonly ICacheProvider _cache;

    public MyService(ICacheProvider cache)
    {
        _cache = cache;
    }

    public async Task<MyObject?> GetCachedObjectAsync(string key)
    {
        // Automatic JSON deserialization
        return await _cache.GetAsync<MyObject>(key);
    }

    public async Task SetCachedObjectAsync(string key, MyObject obj, TimeSpan expiry)
    {
        // Automatic JSON serialization
        await _cache.SetAsync(key, obj, expiry);
    }
}
```

## Configuration Structure

### OElite Standard Configuration

```json
{
  "oelite": {
    "data": {
      "redis": {
        "kortex": "redis.services.localhost:6379,db=0",
        "platform": "redis.services.localhost:6379,db=1",
        "obelisk": "redis.services.localhost:6379,db=2",
        "oesterling": "redis.services.localhost:6379,db=3"
      }
    }
  }
}
```

### Reading Configuration

```csharp
// Kortex server
var redisConnectionString = configuration.GetValue<string>("oelite:data:redis:kortex");

// Platform services
var redisConnectionString = configuration.GetValue<string>("oelite:data:redis:platform");

// Obelisk CRM
var redisConnectionString = configuration.GetValue<string>("oelite:data:redis:obelisk");
```

## Benefits Over Microsoft Packages

### Redis Cache
| Feature | Microsoft.Extensions.Caching.StackExchangeRedis | OElite.Restme.Hosting |
|---------|------------------------------------------------|--------------------------|
| **Abstraction Layer** | Direct StackExchange.Redis dependency | OElite.Restme abstraction |
| **Provider Swapping** | Requires code changes | Config-based provider switching |
| **JSON Serialization** | Manual | Automatic with `ICacheProvider` |
| **Framework Coupling** | Tightly coupled | Decoupled (core packages are framework-agnostic) |
| **OElite Ecosystem** | Not integrated | First-class OElite integration |

### Memory Cache
| Feature | Microsoft.Extensions.Caching.Memory | OElite.Restme.Hosting |
|---------|-------------------------------------|--------------------------|
| **Provider Interface** | IMemoryCache only | Both IMemoryCache + ICacheProvider |
| **JSON Serialization** | Manual object conversion | Automatic with ICacheProvider |
| **Distributed Option** | Separate registration required | Single registration for both |
| **Instance Isolation** | Global instance | Instance-prefixed keys |
| **OElite Ecosystem** | Not integrated | First-class OElite integration |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  ASP.NET Core Application                                    │
│  - Uses IDistributedCache/IMemoryCache (framework abstractions) │
└─────────────┬───────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme.Hosting (This Package)                    │
│  - RedisDistributedCache (IDistributedCache adapter)        │
│  - RestmeMemoryCache (IMemoryCache adapter)                 │
│  - MemoryDistributedCache (in-memory IDistributedCache)     │
│  - ServiceCollectionExtensions (DI registration)            │
└─────────────┬───────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme.Redis / OElite.Restme (Memory)               │
│  - RedisCacheProvider (ICacheProvider implementation)       │
│  - MemoryCacheProvider (ICacheProvider implementation)      │
└─────────────┬───────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme (Core)                                        │
│  - ICacheProvider (abstraction)                              │
│  - RestConfig (configuration)                                │
└─────────────┬───────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────┐
│  StackExchange.Redis / In-Memory                             │
│  - Low-level Redis client / .NET Dictionary                  │
└─────────────────────────────────────────────────────────────┘
```

## Migration from Microsoft.Extensions.Caching.StackExchangeRedis

### Before (Microsoft):

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "myapp:";
});
```

### After (OElite):

```csharp
using OElite.Restme.Hosting.Redis;

services.AddRestmeRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
    options.InstanceName = "myapp:";
});
```

**No other code changes required!** `IDistributedCache` works exactly the same.

## Migration from Microsoft.Extensions.Caching.Memory

### Before (Microsoft):

```csharp
services.AddMemoryCache();
```

### After (OElite):

```csharp
using OElite.Restme.Hosting.Memory;

// Option 1: IMemoryCache only
services.AddRestmeMemoryCache("myapp:");

// Option 2: Both IMemoryCache and IDistributedCache
services.AddRestmeMemoryCacheWithDistributed("myapp:");
```

**No other code changes required!** `IMemoryCache` works exactly the same.

## 🔧 Enhanced Cache Extension Methods

The package provides powerful extension methods that work with both `IDistributedCache` and `IMemoryCache`, offering Rest-style API with advanced features:

### DistributedCacheExtensions

Enhanced methods for `IDistributedCache` with grace period support and query object caching:

#### FindmeAsync Methods

```csharp
// Basic find with refresh action
public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
    string key,
    bool returnExpired = false,
    bool returnInGrace = true,
    Func<T, Task<bool>>? additionalValidation = null,
    Func<Task<T>>? refreshAction = null,
    CancellationToken cancellationToken = default) where T : class;

// Query object-based find with MD5 key generation
public static async Task<T?> FindmeAsync<T>(this IDistributedCache cache,
    object queryObject,
    bool returnExpired = false,
    bool returnInGrace = true,
    Func<T, Task<bool>>? additionalValidation = null,
    Func<Task<T>>? refreshAction = null,
    CancellationToken cancellationToken = default) where T : class;
```

#### CachemeAsync Methods

```csharp
// Basic cache with expiry and grace period
public static async Task CachemeAsync<T>(this IDistributedCache cache,
    string key,
    T data,
    int expiryInSeconds = -1,
    int graceInSeconds = -1,
    CancellationToken cancellationToken = default) where T : class;

// Query object-based cache with MD5 key generation
public static async Task CachemeAsync<T>(this IDistributedCache cache,
    object queryObject,
    T data,
    int expiryInSeconds = -1,
    int graceInSeconds = -1,
    CancellationToken cancellationToken = default) where T : class;
```

#### ExpiremeAsync Methods

```csharp
// Basic expiry with grace period options
public static async Task<bool> ExpiremeAsync(this IDistributedCache cache,
    string key,
    bool invalidateGracePeriod = true,
    CancellationToken cancellationToken = default);

// Query object-based expiry with MD5 key generation
public static async Task<bool> ExpiremeAsync<T>(this IDistributedCache cache,
    object queryObject,
    bool invalidateGracePeriod = true,
    CancellationToken cancellationToken = default);
```

### MemoryCacheExtensions

Enhanced methods for `IMemoryCache` with similar functionality:

#### FindmeAsync Methods

```csharp
// Basic find with refresh action
public static async Task<T?> FindmeAsync<T>(this IMemoryCache cache,
    string key,
    bool returnExpired = false,
    bool returnInGrace = true,
    Func<T, Task<bool>>? additionalValidation = null,
    Func<Task<T>>? refreshAction = null,
    CancellationToken cancellationToken = default) where T : class;

// Query object-based find with MD5 key generation
public static async Task<T?> FindmeAsync<T>(this IMemoryCache cache,
    object queryObject,
    bool returnExpired = false,
    bool returnInGrace = true,
    Func<T, Task<bool>>? additionalValidation = null,
    Func<Task<T>>? refreshAction = null,
    CancellationToken cancellationToken = default) where T : class;
```

#### CachemeAsync Methods

```csharp
// Basic cache with expiry and grace period
public static void CachemeAsync<T>(this IMemoryCache cache,
    string key,
    T data,
    int expiryInSeconds = -1,
    int graceInSeconds = -1) where T : class;

// Query object-based cache with MD5 key generation
public static void CachemeAsync<T>(this IMemoryCache cache,
    object queryObject,
    T data,
    int expiryInSeconds = -1,
    int graceInSeconds = -1) where T : class;
```

#### ExpiremeAsync Methods

```csharp
// Basic expiry with grace period options
public static bool ExpiremeAsync(this IMemoryCache cache,
    string key,
    bool invalidateGracePeriod = true);

// Query object-based expiry with MD5 key generation
public static bool ExpiremeAsync<T>(this IMemoryCache cache,
    object queryObject,
    bool invalidateGracePeriod = true);
```

### 🎯 Advanced Usage Examples

#### Basic Cache Operations

```csharp
public class ProductService
{
    private readonly IDistributedCache _cache;

    public ProductService(IDistributedCache cache) => _cache = cache;

    public async Task<Product?> GetProductAsync(int productId)
    {
        // Try cache first with automatic refresh
        return await _cache.FindmeAsync<Product>(
            key: $"product:{productId}",
            refreshAction: async () => await FetchProductFromDatabase(productId)
        );
    }

    public async Task CacheProductAsync(Product product)
    {
        // Cache with 1 hour expiry and 30 minute grace period
        await _cache.CachemeAsync($"product:{product.Id}", product, 3600, 1800);
    }

    public async Task InvalidateProductAsync(int productId)
    {
        // Expire immediately
        await _cache.ExpiremeAsync($"product:{productId}", invalidateGracePeriod: true);
    }
}
```

#### Query Object Caching

```csharp
public class ProductSearchService
{
    private readonly IDistributedCache _cache;

    public ProductSearchService(IDistributedCache cache) => _cache = cache;

    public async Task<List<Product>> SearchProductsAsync(ProductSearchQuery query)
    {
        // Use query object for automatic MD5 key generation
        return await _cache.FindmeAsync<List<Product>>(
            queryObject: query,
            refreshAction: async () => await ExecuteProductSearch(query)
        );
    }

    public async Task CacheSearchResultsAsync(ProductSearchQuery query, List<Product> results)
    {
        // Cache search results using query object
        await _cache.CachemeAsync(query, results, expiryInSeconds: 1800); // 30 minutes
    }

    public async Task InvalidateSearchAsync<T>(ProductSearchQuery query)
    {
        // Invalidate specific search results
        await _cache.ExpiremeAsync<T>(query, invalidateGracePeriod: true);
    }
}
```

#### Grace Period and Validation

```csharp
public class UserService
{
    private readonly IMemoryCache _cache;

    public UserService(IMemoryCache cache) => _cache = cache;

    public async Task<User?> GetUserAsync(int userId)
    {
        return await _cache.FindmeAsync<User>(
            key: $"user:{userId}",
            returnInGrace: true, // Allow stale data during refresh
            additionalValidation: async (user) =>
            {
                // Custom validation logic
                return user.IsActive && !user.IsDeleted;
            },
            refreshAction: async () => await FetchUserFromDatabase(userId)
        );
    }

    public async Task ExpireUserWithGraceAsync(int userId)
    {
        // Expire but allow grace period for smooth refresh
        _cache.ExpiremeAsync($"user:{userId}", invalidateGracePeriod: false);
    }
}
```

### 🔑 Key Features

#### 1. Grace Period Support
- **Expiry vs Grace**: Items can be expired but still served during grace period
- **Background Refresh**: Automatic refresh triggers when items expire but are still in grace
- **Smooth UX**: Users never experience cache misses during refresh

#### 2. Query Object Caching
- **Automatic MD5 Keys**: Complex query objects automatically generate consistent cache keys
- **Type Safety**: Generic constraints ensure proper type handling
- **Serialization**: JSON serialization of query objects for key generation

#### 3. Validation and Refresh
- **Custom Validation**: Optional validation functions for cached data
- **Automatic Refresh**: Background refresh actions when cache misses or validation fails
- **Fallback Strategy**: Graceful degradation when refresh actions fail

#### 4. Rest-Style API Consistency
- **Familiar Methods**: `FindmeAsync`, `CachemeAsync`, `ExpiremeAsync` match Rest patterns
- **Consistent Parameters**: Same parameter patterns across all cache providers
- **Type Safety**: Generic constraints ensure only Rest implementation types

### 🚀 Performance Benefits

- **Reduced Database Load**: Grace period reduces database hits during refresh
- **Better User Experience**: Stale data served instantly while fresh data loads in background
- **Efficient Key Management**: MD5 hashing ensures consistent, collision-resistant keys
- **Memory Efficient**: Proper expiry handling prevents memory leaks

### 🛡️ Error Handling

The extension methods include comprehensive error handling:

```csharp
// Automatic fallback on cache failures
public async Task<Product?> GetProductSafelyAsync(int productId)
{
    try
    {
        return await _cache.FindmeAsync<Product>(
            $"product:{productId}",
            refreshAction: () => _repository.GetByIdAsync(productId)
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cache operation failed for product {ProductId}", productId);
        return await _repository.GetByIdAsync(productId); // Direct fallback
    }
}
```

## Future Extensions

This package will be expanded to include:

- **Storage extensions**: `IFileStorage` integration for S3/Azure providers
- **Queue extensions**: Background job processing with RabbitMQ
- **Health checks**: ASP.NET Core health check integrations
- **Middleware**: Request caching, response compression, etc.

## Related Packages

- **OElite.Restme** - Core abstractions and interfaces
- **OElite.Restme.Redis** - Redis cache provider
- **OElite.Restme.RabbitMQ** - RabbitMQ queue provider
- **OElite.Restme.S3** - S3 storage provider

## License

Copyright © OElite Limited. All rights reserved.
