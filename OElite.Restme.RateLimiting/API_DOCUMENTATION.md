# OElite.Restme.RateLimiting API Documentation

## Overview

This document provides comprehensive API reference for the OElite.Restme.RateLimiting package, including all classes, interfaces, methods, and configuration options.

## Namespaces

- `OElite.Restme.RateLimiting.Models` - Data models and configuration classes
- `OElite.Restme.RateLimiting.Services` - Core rate limiting services
- `OElite.Restme.RateLimiting.Interfaces` - Service interfaces
- `OElite.Restme.RateLimiting.Storage` - Storage implementations
- `OElite.Restme.RateLimiting.Extensions` - Service collection extensions
- `OElite.Restme.RateLimiting.Middleware` - ASP.NET Core middleware

---

## Models

### RateLimitOptions

Configuration options for rate limiting.

```csharp
public class RateLimitOptions
{
    // Basic Configuration
    public int Limit { get; set; } = 100;
    public int WindowInSeconds { get; set; } = 60;
    public string? KeyPrefix { get; set; }
    public bool Enabled { get; set; } = true;
    public int StatusCode { get; set; } = 429;
    public string ErrorMessage { get; set; } = "Rate limit exceeded. Too many requests.";
    public bool IncludeHeaders { get; set; } = true;
    
    // Storage Configuration
    public RateLimitStorageType StorageType { get; set; } = RateLimitStorageType.Memory;
    public string? RedisConnectionString { get; set; }
    public string RedisKeyPrefix { get; set; } = "rate_limit:";
    
    // Advanced Features
    public int BurstCapacity { get; set; } = 200;
    public bool EnableDDoSProtection { get; set; } = true;
    public int DDoSThreshold { get; set; } = 1000;
    public int DDoSBlockMinutes { get; set; } = 60;
    public bool ProgressiveBlocking { get; set; } = true;
    public bool EnableAdaptiveLimiting { get; set; } = false;
    public double ServerLoadThreshold { get; set; } = 0.6;
    public EmergencyModeConfig EmergencyMode { get; set; } = new();
    public bool EnableDomainRateLimiting { get; set; } = false;
    public int DomainRequestsPerMinute { get; set; } = 1000;
    public int DomainBurstCapacity { get; set; } = 2000;
    public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Limit` | `int` | `100` | Maximum requests allowed per window |
| `WindowInSeconds` | `int` | `60` | Duration of the time window in seconds |
| `KeyPrefix` | `string?` | `null` | Prefix for rate limit keys |
| `Enabled` | `bool` | `true` | Whether rate limiting is enabled |
| `StatusCode` | `int` | `429` | HTTP status code for rate limit exceeded |
| `ErrorMessage` | `string` | `"Rate limit exceeded..."` | Error message for blocked requests |
| `IncludeHeaders` | `bool` | `true` | Whether to include rate limit headers |
| `StorageType` | `RateLimitStorageType` | `Memory` | Storage backend type |
| `RedisConnectionString` | `string?` | `null` | Redis connection string |
| `RedisKeyPrefix` | `string` | `"rate_limit:"` | Redis key prefix |
| `BurstCapacity` | `int` | `200` | Token bucket burst capacity |
| `EnableDDoSProtection` | `bool` | `true` | Enable DDoS protection |
| `DDoSThreshold` | `int` | `1000` | DDoS detection threshold |
| `DDoSBlockMinutes` | `int` | `60` | DDoS block duration in minutes |
| `ProgressiveBlocking` | `bool` | `true` | Enable progressive blocking |
| `EnableAdaptiveLimiting` | `bool` | `false` | Enable adaptive rate limiting |
| `ServerLoadThreshold` | `double` | `0.6` | Server load threshold for adaptation |
| `EmergencyMode` | `EmergencyModeConfig` | `new()` | Emergency mode configuration |
| `EnableDomainRateLimiting` | `bool` | `false` | Enable domain-based rate limiting |
| `DomainRequestsPerMinute` | `int` | `1000` | Domain rate limit per minute |
| `DomainBurstCapacity` | `int` | `2000` | Domain burst capacity |
| `Algorithm` | `RateLimitAlgorithm` | `TokenBucket` | Rate limiting algorithm |

### RateLimitResult

Result of a rate limit check.

```csharp
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public long CurrentCount { get; set; }
    public long Limit { get; set; }
    public long WindowRemainingSeconds { get; set; }
    public DateTime WindowResetTime { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public long RemainingRequests { get; set; }
    public int RetryAfterSeconds { get; set; }
    public string BlockReason { get; set; } = string.Empty;
    public DateTime ResetTimeUtc { get; set; }
}
```

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `IsAllowed` | `bool` | Whether the request is allowed |
| `CurrentCount` | `long` | Current request count in window |
| `Limit` | `long` | Maximum allowed requests |
| `WindowRemainingSeconds` | `long` | Seconds remaining in window |
| `WindowResetTime` | `DateTime` | When the window resets |
| `Key` | `string` | Rate limit key used |
| `IsBlocked` | `bool` | Whether request was blocked |
| `RemainingRequests` | `long` | Requests remaining in window |
| `RetryAfterSeconds` | `int` | Seconds to wait before retry |
| `BlockReason` | `string` | Reason for blocking |
| `ResetTimeUtc` | `DateTime` | When block expires |

### EmergencyModeConfig

Configuration for emergency mode activation.

```csharp
public class EmergencyModeConfig
{
    public bool Enabled { get; set; } = true;
    public int TriggerThreshold { get; set; } = 5000;
    public int BlockDurationHours { get; set; } = 24;
    public bool SendAlerts { get; set; } = true;
}
```

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Whether emergency mode is enabled |
| `TriggerThreshold` | `int` | `5000` | Request threshold to trigger emergency mode |
| `BlockDurationHours` | `int` | `24` | Block duration in hours for emergency mode |
| `SendAlerts` | `bool` | `true` | Whether to send alerts on activation |

---

## Enums

### RateLimitStorageType

Storage backend types.

```csharp
public enum RateLimitStorageType
{
    Memory,  // In-memory storage (single instance)
    Redis    // Redis distributed storage (multi-instance)
}
```

### RateLimitAlgorithm

Rate limiting algorithms.

```csharp
public enum RateLimitAlgorithm
{
    FixedWindow,    // Fixed time windows
    TokenBucket,    // Token bucket with burst capacity
    SlidingWindow,  // Continuous sliding window
    LeakyBucket     // Leaky bucket for traffic shaping
}
```

---

## Interfaces

### IRateLimitService

Core rate limiting service interface.

```csharp
public interface IRateLimitService
{
    Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options);
}
```

**Methods:**

| Method | Description |
|--------|-------------|
| `CheckRateLimitAsync(HttpContext context, RateLimitOptions options)` | Check if request is within rate limit |

### IRateLimitStore

Storage interface for rate limiting data.

```csharp
public interface IRateLimitStore
{
    // Token Bucket
    Task<TokenBucket?> GetTokenBucketAsync(string key);
    Task SetTokenBucketAsync(TokenBucket bucket);
    
    // Fixed Window
    Task<FixedWindow?> GetFixedWindowAsync(string key);
    Task SetFixedWindowAsync(FixedWindow window);
    
    // Sliding Window
    Task<SlidingWindow?> GetSlidingWindowAsync(string key);
    Task SetSlidingWindowAsync(string key, SlidingWindow window);
    
    // Leaky Bucket
    Task<LeakyBucket?> GetLeakyBucketAsync(string key);
    Task SetLeakyBucketAsync(LeakyBucket bucket);
}
```

**Methods:**

| Method | Description |
|--------|-------------|
| `GetTokenBucketAsync(string key)` | Get token bucket for key |
| `SetTokenBucketAsync(TokenBucket bucket)` | Set token bucket |
| `GetFixedWindowAsync(string key)` | Get fixed window for key |
| `SetFixedWindowAsync(FixedWindow window)` | Set fixed window |
| `GetSlidingWindowAsync(string key)` | Get sliding window for key |
| `SetSlidingWindowAsync(string key, SlidingWindow window)` | Set sliding window |
| `GetLeakyBucketAsync(string key)` | Get leaky bucket for key |
| `SetLeakyBucketAsync(LeakyBucket bucket)` | Set leaky bucket |

---

## Services

### AdvancedRateLimitService

Enterprise-grade rate limiting service with advanced features.

```csharp
public class AdvancedRateLimitService : IRateLimitService, IDisposable
{
    public AdvancedRateLimitService(
        IRateLimitStore store,
        ILogger<AdvancedRateLimitService> logger,
        IMemoryCache memoryCache);
    
    public Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options);
    public void Dispose();
}
```

**Constructor Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `store` | `IRateLimitStore` | Storage implementation |
| `logger` | `ILogger<AdvancedRateLimitService>` | Logger instance |
| `memoryCache` | `IMemoryCache` | Memory cache for temporary data |

**Methods:**

| Method | Description |
|--------|-------------|
| `CheckRateLimitAsync(HttpContext context, RateLimitOptions options)` | Check rate limit with advanced features |
| `Dispose()` | Clean up resources |

**Features:**
- DDoS protection with sliding window detection
- Progressive blocking based on attack severity
- Emergency mode for severe attacks
- Adaptive rate limiting based on server load
- Domain-based rate limiting
- Multiple algorithm support

---

## Storage Implementations

### MemoryRateLimitStore

In-memory storage implementation.

```csharp
public class MemoryRateLimitStore : IRateLimitStore
{
    public MemoryRateLimitStore(IMemoryCache cache);
    
    // Implements all IRateLimitStore methods
}
```

**Constructor Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `cache` | `IMemoryCache` | Memory cache instance |

### RedisRateLimitStore

Redis distributed storage implementation.

```csharp
public class RedisRateLimitStore : IRateLimitStore
{
    public RedisRateLimitStore(IDatabase database);
    
    // Implements all IRateLimitStore methods
}
```

**Constructor Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `database` | `IDatabase` | Redis database instance |

---

## Extensions

### RateLimitingServiceCollectionExtensions

Service collection extension methods.

```csharp
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services, 
        Action<RateLimitOptions>? configureOptions = null);
    
    public static IServiceCollection AddRateLimitingWithMemoryStorage(
        this IServiceCollection services, 
        Action<RateLimitOptions>? configureOptions = null);
    
    public static IServiceCollection AddRateLimitingWithRedisStorage(
        this IServiceCollection services, 
        Action<RateLimitOptions>? configureOptions = null);
}
```

**Methods:**

| Method | Description |
|--------|-------------|
| `AddRateLimiting(IServiceCollection services, Action<RateLimitOptions>? configureOptions)` | Add rate limiting with default memory storage |
| `AddRateLimitingWithMemoryStorage(IServiceCollection services, Action<RateLimitOptions>? configureOptions)` | Add rate limiting with memory storage |
| `AddRateLimitingWithRedisStorage(IServiceCollection services, Action<RateLimitOptions>? configureOptions)` | Add rate limiting with Redis storage |

### RateLimitingMiddlewareExtensions

Middleware extension methods.

```csharp
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app);
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, Action<RateLimitOptions> configureOptions);
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, string path, Action<RateLimitOptions> configureOptions);
}
```

**Methods:**

| Method | Description |
|--------|-------------|
| `UseRateLimiting(IApplicationBuilder app)` | Add rate limiting middleware |
| `UseRateLimiting(IApplicationBuilder app, Action<RateLimitOptions> configureOptions)` | Add rate limiting with configuration |
| `UseRateLimiting(IApplicationBuilder app, string path, Action<RateLimitOptions> configureOptions)` | Add rate limiting for specific path |

---

## Middleware

### RateLimitMiddleware

ASP.NET Core middleware for rate limiting.

```csharp
public class RateLimitMiddleware
{
    public RateLimitMiddleware(RequestDelegate next, RateLimitOptions options);
    
    public async Task InvokeAsync(HttpContext context);
}
```

**Constructor Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `next` | `RequestDelegate` | Next middleware in pipeline |
| `options` | `RateLimitOptions` | Rate limiting configuration |

**Methods:**

| Method | Description |
|--------|-------------|
| `InvokeAsync(HttpContext context)` | Process HTTP request for rate limiting |

---

## Data Models

### TokenBucket

Token bucket data structure.

```csharp
public class TokenBucket
{
    public string Key { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public double Tokens { get; set; }
    public DateTime LastRefillTime { get; set; }
    public double RefillRate { get; set; }
}
```

### FixedWindow

Fixed window data structure.

```csharp
public class FixedWindow
{
    public string Key { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
}
```

### SlidingWindow

Sliding window data structure.

```csharp
public class SlidingWindow
{
    public SlidingWindow(int windowSizeSeconds);
    
    public int RequestCount { get; }
    public void AddRequest(DateTime timestamp);
    public void RemoveOldRequests(DateTime cutoff);
    public int GetRequestCount(DateTime now);
}
```

### LeakyBucket

Leaky bucket data structure.

```csharp
public class LeakyBucket
{
    public string Key { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public double Level { get; set; }
    public DateTime LastLeakTime { get; set; }
    public double LeakRate { get; set; }
}
```

---

## Configuration Examples

### Basic Configuration

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 100;
    options.WindowInSeconds = 60;
    options.StorageType = RateLimitStorageType.Memory;
});
```

### Advanced Configuration

```csharp
builder.Services.AddRateLimiting(options =>
{
    // Basic settings
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 2000;
    
    // DDoS Protection
    options.EnableDDoSProtection = true;
    options.DDoSThreshold = 5000;
    options.DDoSBlockMinutes = 60;
    options.ProgressiveBlocking = true;
    
    // Emergency Mode
    options.EmergencyMode.Enabled = true;
    options.EmergencyMode.TriggerThreshold = 10000;
    options.EmergencyMode.BlockDurationHours = 24;
    
    // Adaptive Limiting
    options.EnableAdaptiveLimiting = true;
    options.ServerLoadThreshold = 0.7;
    
    // Domain-based limiting
    options.EnableDomainRateLimiting = true;
    options.DomainRequestsPerMinute = 5000;
    
    // Algorithm
    options.Algorithm = RateLimitAlgorithm.TokenBucket;
    
    // Redis
    options.StorageType = RateLimitStorageType.Redis;
    options.RedisConnectionString = "localhost:6379";
});
```

### Per-Endpoint Configuration

```csharp
app.UseRateLimiting("/api/public", options =>
{
    options.Limit = 100;
    options.WindowInSeconds = 60;
});

app.UseRateLimiting("/api/premium", options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 2000;
});
```

---

## Error Handling

### RateLimitExceededException

Exception thrown when rate limit is exceeded.

```csharp
public class RateLimitExceededException : Exception
{
    public RateLimitResult Result { get; }
    
    public RateLimitExceededException(RateLimitResult result) : base(result.BlockReason)
    {
        Result = result;
    }
}
```

### Graceful Degradation

The service implements graceful degradation - when rate limiting fails, requests are allowed to prevent service disruption.

---

## Performance Considerations

### Memory Usage

| Algorithm | Memory per Key | Notes |
|-----------|----------------|-------|
| Token Bucket | ~100 bytes | Minimal memory usage |
| Fixed Window | ~50 bytes | Very efficient |
| Sliding Window | ~1KB | Higher memory usage |
| Leaky Bucket | ~100 bytes | Similar to token bucket |

### Redis Performance

| Operation | Latency | Throughput |
|-----------|---------|------------|
| Get | ~1ms | 10,000+ ops/sec |
| Set | ~1ms | 10,000+ ops/sec |
| Pipeline | ~2ms | 50,000+ ops/sec |

### Recommendations

1. **Use Redis for >1000 concurrent users**
2. **Monitor memory usage in production**
3. **Set appropriate cleanup intervals**
4. **Use connection pooling for Redis**
5. **Consider algorithm choice based on traffic patterns**

---

## Security Considerations

### IP Address Handling

The service properly handles various IP address scenarios:

- **Direct connections**: Uses `HttpContext.Connection.RemoteIpAddress`
- **Proxy connections**: Checks `X-Forwarded-For` header
- **Load balancer**: Checks `X-Real-IP` header
- **IPv6 support**: Full IPv6 address support

### Configuration Security

- Use environment variables for sensitive configuration
- Validate Redis connection strings
- Implement proper key prefixes to avoid conflicts
- Monitor for configuration drift

### Attack Mitigation

The service provides multiple layers of protection:

1. **Rate Limiting**: Basic request rate control
2. **DDoS Protection**: Advanced attack detection
3. **Emergency Mode**: Severe attack response
4. **Progressive Blocking**: Escalating protection
5. **Adaptive Limiting**: Server load protection

---

## Monitoring and Observability

### Logging

The service provides comprehensive logging:

```csharp
// Log levels
_logger.LogDebug("Rate limit check for {Key}", key);
_logger.LogWarning("Rate limit exceeded for {Key}", key);
_logger.LogError("Rate limiting failed for {Key}", key);
_logger.LogCritical("Emergency mode activated for {Key}", key);
```

### Metrics

Key metrics to monitor:

- `rate_limit_requests_total`
- `rate_limit_blocks_total`
- `rate_limit_ddos_blocks_total`
- `rate_limit_emergency_activations_total`
- `rate_limit_server_load`

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<RateLimitHealthCheck>("rate_limiting");
```

---

## Testing

### Unit Testing

```csharp
[Test]
public async Task ShouldAllowRequestWithinLimit()
{
    var service = new AdvancedRateLimitService(store, logger, cache);
    var options = new RateLimitOptions { Limit = 10, WindowInSeconds = 60 };
    
    var result = await service.CheckRateLimitAsync(context, options);
    
    Assert.IsTrue(result.IsAllowed);
    Assert.AreEqual(1, result.CurrentCount);
}
```

### Integration Testing

```csharp
[Test]
public async Task ShouldBlockRequestAfterLimit()
{
    var client = _factory.CreateClient();
    
    // Make requests up to limit
    for (int i = 0; i < 10; i++)
    {
        var response = await client.GetAsync("/api/test");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
    
    // Next request should be blocked
    var blockedResponse = await client.GetAsync("/api/test");
    Assert.AreEqual(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);
}
```

---

## Migration Guide

### From v1.0.0 to v2.0.0

1. **Update configuration**:
   ```csharp
   // Old
   options.RequestsPerMinute = 100;
   
   // New
   options.Limit = 100;
   options.WindowInSeconds = 60;
   ```

2. **Add new features**:
   ```csharp
   options.EnableDDoSProtection = true;
   options.EnableAdaptiveLimiting = true;
   ```

3. **Update storage**:
   ```csharp
   // Old
   options.UseRedis = true;
   
   // New
   options.StorageType = RateLimitStorageType.Redis;
   ```

---

## Troubleshooting

### Common Issues

1. **Rate limiting not working**
   - Check middleware order
   - Verify configuration
   - Check logs for errors

2. **Redis connection issues**
   - Verify connection string
   - Check Redis server status
   - Test connection manually

3. **High memory usage**
   - Enable cleanup
   - Reduce window size
   - Use Redis for distributed scenarios

4. **Performance issues**
   - Use Redis for high traffic
   - Optimize algorithm choice
   - Monitor server load

### Debug Configuration

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

---

## Support

For additional support:

- **Documentation**: [GitHub Wiki](https://github.com/PhanesDigital/oelite/wiki)
- **Issues**: [GitHub Issues](https://github.com/PhanesDigital/oelite/issues)
- **Discussions**: [GitHub Discussions](https://github.com/PhanesDigital/oelite/discussions)
