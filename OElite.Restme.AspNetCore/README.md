# OElite.Restme.AspNetCore

ASP.NET Core integration extensions for **OElite.Restme** - provides dependency injection, IDistributedCache adapters, and middleware for seamless integration with ASP.NET Core applications.

## Purpose

This package decouples ASP.NET Core-specific extensions from the core Restme packages, keeping the core libraries framework-agnostic while providing first-class ASP.NET Core support.

## Features

- ✅ **IDistributedCache adapter** for OElite.Restme.Redis
- ✅ **Dependency injection extensions** for all Restme providers
- ✅ **Clean separation** between core abstractions and framework integrations
- ✅ **Multi-framework support** (net8.0, net9.0, net10.0)

## Installation

```bash
dotnet add package OElite.Restme.AspNetCore
```

## Usage

### Redis Distributed Cache

Replace `Microsoft.Extensions.Caching.StackExchangeRedis` with OElite's Redis provider:

```csharp
using OElite.Restme.AspNetCore.Redis;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Option 1: Simple configuration
        builder.Services.AddOEliteRedisCache(
            connectionString: "localhost:6379",
            instanceName: "myapp:"
        );

        // Option 2: Configuration-based setup
        builder.Services.AddOEliteRedisCache(options =>
        {
            options.ConnectionString = builder.Configuration["oelite:data:redis:platform"];
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

| Feature | Microsoft.Extensions.Caching.StackExchangeRedis | OElite.Restme.AspNetCore |
|---------|------------------------------------------------|--------------------------|
| **Abstraction Layer** | Direct StackExchange.Redis dependency | OElite.Restme abstraction |
| **Provider Swapping** | Requires code changes | Config-based provider switching |
| **JSON Serialization** | Manual | Automatic with `ICacheProvider` |
| **Framework Coupling** | Tightly coupled | Decoupled (core packages are framework-agnostic) |
| **OElite Ecosystem** | Not integrated | First-class OElite integration |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  ASP.NET Core Application                                    │
│  - Uses IDistributedCache (framework abstraction)           │
└──────────────┬──────────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme.AspNetCore (This Package)                    │
│  - RedisDistributedCache (IDistributedCache adapter)        │
│  - ServiceCollectionExtensions (DI registration)            │
└──────────────┬──────────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme.Redis                                         │
│  - RedisCacheProvider (ICacheProvider implementation)       │
└──────────────┬──────────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│  OElite.Restme (Core)                                        │
│  - ICacheProvider (abstraction)                              │
│  - RestConfig (configuration)                                │
└──────────────┬──────────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────────┐
│  StackExchange.Redis                                         │
│  - Low-level Redis client                                    │
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
using OElite.Restme.AspNetCore.Redis;

services.AddOEliteRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
    options.InstanceName = "myapp:";
});
```

**No other code changes required!** `IDistributedCache` works exactly the same.

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
