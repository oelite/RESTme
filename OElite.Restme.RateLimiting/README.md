# OElite.Restme.RateLimiting

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.RateLimiting.svg)](https://www.nuget.org/packages/OElite.Restme.RateLimiting/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)

**Enterprise-grade rate limiting middleware for ASP.NET Core applications with advanced DDoS protection, adaptive limiting, and distributed storage support.**

## 🚀 Features

### Core Rate Limiting
- **Multiple Algorithms**: Fixed Window, Token Bucket, Sliding Window, Leaky Bucket
- **Distributed Storage**: Redis support for multi-instance deployments
- **Flexible Configuration**: Per-endpoint, per-IP, per-domain rate limiting
- **Standard Compliance**: RFC 6585 compliant with proper HTTP headers

### Advanced Security Features
- **DDoS Protection**: Sliding window detection with progressive blocking
- **Emergency Mode**: Automatic severe attack response with extended blocking
- **Adaptive Limiting**: Server load-based rate adjustment
- **IP Blocking**: Temporary and permanent IP blocking capabilities

### Enterprise Features
- **High Performance**: Optimized algorithms with minimal memory footprint
- **Observability**: Comprehensive logging and metrics integration
- **Fail-Safe**: Graceful degradation when rate limiting fails
- **Thread-Safe**: Concurrent request handling with lock-free operations

## 📦 Installation

```bash
# Basic installation
dotnet add package OElite.Restme.RateLimiting

# With Redis support
dotnet add package OElite.Restme.RateLimiting
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

## 🏗️ Quick Start

### Basic Setup

```csharp
// Program.cs
using OElite.Restme.RateLimiting.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add rate limiting services
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 100;
    options.WindowInSeconds = 60;
    options.StorageType = RateLimitStorageType.Memory;
});

var app = builder.Build();

// Add rate limiting middleware
app.UseRateLimiting();

app.MapControllers();
app.Run();
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
    
    // Algorithm selection
    options.Algorithm = RateLimitAlgorithm.TokenBucket;
    
    // Redis for distributed scenarios
    options.StorageType = RateLimitStorageType.Redis;
    options.RedisConnectionString = "localhost:6379";
});
```

## 🔧 Configuration Options

### RateLimitOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Limit` | `int` | `100` | Maximum requests per window |
| `WindowInSeconds` | `int` | `60` | Time window duration |
| `BurstCapacity` | `int` | `200` | Token bucket burst capacity |
| `Algorithm` | `RateLimitAlgorithm` | `TokenBucket` | Rate limiting algorithm |
| `StorageType` | `RateLimitStorageType` | `Memory` | Storage backend |
| `EnableDDoSProtection` | `bool` | `true` | Enable DDoS detection |
| `DDoSThreshold` | `int` | `1000` | DDoS detection threshold |
| `DDoSBlockMinutes` | `int` | `60` | DDoS block duration |
| `ProgressiveBlocking` | `bool` | `true` | Escalating block duration |
| `EnableAdaptiveLimiting` | `bool` | `false` | Server load-based adjustment |
| `ServerLoadThreshold` | `double` | `0.6` | Load threshold for adaptation |
| `EmergencyMode` | `EmergencyModeConfig` | See below | Emergency mode settings |

### EmergencyModeConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enable emergency mode |
| `TriggerThreshold` | `int` | `5000` | Requests to trigger emergency |
| `BlockDurationHours` | `int` | `24` | Emergency block duration |
| `SendAlerts` | `bool` | `true` | Send alerts on activation |

## 🎯 Rate Limiting Algorithms

### Token Bucket (Default)
- **Best for**: APIs with burst traffic
- **Behavior**: Allows short bursts above normal rate
- **Configuration**: Set `BurstCapacity` for burst allowance

```csharp
options.Algorithm = RateLimitAlgorithm.TokenBucket;
options.BurstCapacity = 200; // Allow 200 request bursts
```

### Fixed Window
- **Best for**: Simple rate limiting
- **Behavior**: Fixed time windows with hard limits
- **Configuration**: Set `WindowInSeconds` for window size

```csharp
options.Algorithm = RateLimitAlgorithm.FixedWindow;
options.WindowInSeconds = 60; // 1-minute windows
```

### Sliding Window
- **Best for**: Smooth rate limiting
- **Behavior**: Continuous sliding time window
- **Configuration**: More memory intensive but smoother

```csharp
options.Algorithm = RateLimitAlgorithm.SlidingWindow;
```

### Leaky Bucket
- **Best for**: Traffic shaping
- **Behavior**: Constant output rate regardless of input bursts
- **Configuration**: Good for smoothing traffic spikes

```csharp
options.Algorithm = RateLimitAlgorithm.LeakyBucket;
```

## 🛡️ DDoS Protection

### Automatic Detection
The package automatically detects DDoS patterns using sliding window analysis:

```csharp
options.EnableDDoSProtection = true;
options.DDoSThreshold = 1000;        // 1000 requests per window
options.DDoSBlockMinutes = 60;       // Block for 1 hour
options.ProgressiveBlocking = true;  // Escalate block duration
```

### Progressive Blocking
When `ProgressiveBlocking` is enabled, block duration increases with attack severity:

- **2x severity**: 2x block time
- **3x severity**: 4x block time
- **Emergency mode**: 24-hour blocks

### Emergency Mode
For severe attacks, emergency mode provides extended protection:

```csharp
options.EmergencyMode.Enabled = true;
options.EmergencyMode.TriggerThreshold = 10000;  // 10k requests
options.EmergencyMode.BlockDurationHours = 24;   // 24-hour block
```

## 🔄 Adaptive Rate Limiting

Automatically adjusts rate limits based on server load:

```csharp
options.EnableAdaptiveLimiting = true;
options.ServerLoadThreshold = 0.7;  // 70% CPU threshold
```

**Behavior**:
- **Low load**: Normal rate limits
- **High load**: Reduced rate limits to protect server
- **Critical load**: Minimal rate limits

## 🌐 Distributed Storage

### Redis Configuration
For multi-instance deployments, use Redis for shared state:

```csharp
options.StorageType = RateLimitStorageType.Redis;
options.RedisConnectionString = "localhost:6379";
options.RedisKeyPrefix = "myapp:rate_limit:";
```

### Memory Storage
For single-instance applications:

```csharp
options.StorageType = RateLimitStorageType.Memory;
```

## 📊 Response Headers

The middleware automatically adds standard rate limiting headers:

```
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1640995200
X-RateLimit-Window: 60
Retry-After: 60
```

## 🎛️ Per-Endpoint Configuration

Configure different rate limits for different endpoints:

```csharp
// Program.cs
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

## 🔍 Monitoring and Logging

### Logging Configuration
```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});
```

### Metrics Integration
The package provides metrics for monitoring:

- `rate_limit_requests_total`: Total requests processed
- `rate_limit_blocks_total`: Total blocked requests
- `rate_limit_ddos_blocks_total`: DDoS blocks
- `rate_limit_emergency_activations_total`: Emergency mode activations

## 🚨 Error Handling

### Graceful Degradation
When rate limiting fails, the middleware fails open (allows requests) to prevent service disruption:

```csharp
// Automatic fallback behavior
try
{
    var result = await CheckRateLimitAsync(context, options);
    return result;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Rate limiting failed, allowing request");
    return new RateLimitResult { IsAllowed = true };
}
```

### Custom Error Responses
Customize error responses when rate limits are exceeded:

```csharp
options.StatusCode = 429;
options.ErrorMessage = "Rate limit exceeded. Please try again later.";
```

## 🧪 Testing

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

## 🔧 Advanced Usage

### Custom Key Generation
```csharp
app.UseRateLimiting(options =>
{
    options.KeyPrefix = "custom";
    // Custom key will be: custom:{client_ip}
});
```

### Multiple Rate Limits
```csharp
// IP-based limiting
app.UseRateLimiting("/api", options =>
{
    options.Limit = 100;
    options.KeyPrefix = "ip";
});

// User-based limiting
app.UseRateLimiting("/api/user", options =>
{
    options.Limit = 1000;
    options.KeyPrefix = "user";
});
```

### Custom Storage Implementation
```csharp
public class CustomRateLimitStore : IRateLimitStore
{
    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        // Custom storage implementation
    }
    
    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        // Custom storage implementation
    }
}

// Register custom store
builder.Services.AddSingleton<IRateLimitStore, CustomRateLimitStore>();
```

## 📈 Performance Considerations

### Memory Usage
- **Token Bucket**: ~100 bytes per key
- **Sliding Window**: ~1KB per key (with 1000 requests)
- **Fixed Window**: ~50 bytes per key

### Redis Performance
- **Latency**: ~1-2ms per operation
- **Throughput**: 10,000+ operations/second
- **Memory**: ~1KB per key

### Recommendations
- Use Redis for >1000 concurrent users
- Monitor memory usage in production
- Set appropriate cleanup intervals
- Use connection pooling for Redis

## 🔒 Security Best Practices

### IP Address Handling
```csharp
// Always use real client IP (not proxy IP)
var clientIp = GetClientIpAddress(context);

// Handle IPv6 correctly
if (IPAddress.TryParse(clientIp, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
{
    // IPv6 specific handling
}
```

### Configuration Security
```csharp
// Use environment variables for sensitive config
options.RedisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
```

### Monitoring Security Events
```csharp
// Log security events
_logger.LogWarning("DDoS attack detected from {ClientIp}", clientIp);
```

## 🐛 Troubleshooting

### Common Issues

#### Rate Limiting Not Working
```csharp
// Check middleware order
app.UseRateLimiting(); // Must be before app.MapControllers()
app.MapControllers();
```

#### Redis Connection Issues
```csharp
// Verify Redis connection
var connection = ConnectionMultiplexer.Connect("localhost:6379");
var db = connection.GetDatabase();
var pong = await db.PingAsync();
```

#### Memory Usage High
```csharp
// Enable cleanup
options.EnableCleanup = true;
options.CleanupIntervalMinutes = 5;
```

### Debug Logging
```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
```

## 📚 API Reference

### IRateLimitService
```csharp
public interface IRateLimitService
{
    Task<RateLimitResult> CheckRateLimitAsync(HttpContext context, RateLimitOptions options);
}
```

### RateLimitResult
```csharp
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public bool IsBlocked { get; set; }
    public string Key { get; set; }
    public long CurrentCount { get; set; }
    public long Limit { get; set; }
    public long RemainingRequests { get; set; }
    public int RetryAfterSeconds { get; set; }
    public string BlockReason { get; set; }
    public DateTime ResetTimeUtc { get; set; }
}
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup
```bash
git clone https://github.com/PhanesDigital/oelite.git
cd oelite/uranus/restme/OElite.Restme.RateLimiting
dotnet restore
dotnet build
dotnet test
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **Documentation**: [GitHub Wiki](https://github.com/PhanesDigital/oelite/wiki)
- **Issues**: [GitHub Issues](https://github.com/PhanesDigital/oelite/issues)
- **Discussions**: [GitHub Discussions](https://github.com/PhanesDigital/oelite/discussions)

## 🏷️ Version History

### v2.0.0 (Current)
- ✅ Advanced DDoS protection
- ✅ Emergency mode
- ✅ Adaptive rate limiting
- ✅ Multiple algorithms
- ✅ Redis distributed storage
- ✅ Comprehensive logging

### v1.0.0
- ✅ Basic rate limiting
- ✅ Memory storage
- ✅ Fixed window algorithm

---

**Made with ❤️ by the OElite Development Team**
