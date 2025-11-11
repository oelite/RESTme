# OElite.Restme.RateLimiting Examples

This document provides comprehensive examples for using the OElite.Restme.RateLimiting package in various scenarios.

## Table of Contents

1. [Basic Setup](#basic-setup)
2. [Advanced Configuration](#advanced-configuration)
3. [Per-Endpoint Rate Limiting](#per-endpoint-rate-limiting)
4. [DDoS Protection](#ddos-protection)
5. [Adaptive Rate Limiting](#adaptive-rate-limiting)
6. [Redis Distributed Storage](#redis-distributed-storage)
7. [Custom Storage Implementation](#custom-storage-implementation)
8. [Testing Examples](#testing-examples)
9. [Production Deployment](#production-deployment)
10. [Troubleshooting Examples](#troubleshooting-examples)

---

## Basic Setup

### Minimal Configuration

```csharp
// Program.cs
using OElite.Restme.RateLimiting.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add basic rate limiting
builder.Services.AddRateLimiting();

var app = builder.Build();

// Add middleware
app.UseRateLimiting();

app.MapControllers();
app.Run();
```

### Custom Basic Configuration

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 100;                    // 100 requests
    options.WindowInSeconds = 60;           // per minute
    options.StatusCode = 429;               // Too Many Requests
    options.ErrorMessage = "Rate limit exceeded. Please try again later.";
    options.IncludeHeaders = true;          // Include rate limit headers
});
```

---

## Advanced Configuration

### Enterprise-Grade Setup

```csharp
builder.Services.AddRateLimiting(options =>
{
    // Basic Rate Limiting
    options.Limit = 1000;                   // 1000 requests per window
    options.WindowInSeconds = 60;           // 1-minute windows
    options.BurstCapacity = 2000;           // Allow 2000 request bursts
    options.Algorithm = RateLimitAlgorithm.TokenBucket;
    
    // DDoS Protection
    options.EnableDDoSProtection = true;
    options.DDoSThreshold = 5000;            // 5000 requests trigger DDoS protection
    options.DDoSBlockMinutes = 60;           // Block for 1 hour
    options.ProgressiveBlocking = true;      // Escalate block duration
    
    // Emergency Mode
    options.EmergencyMode.Enabled = true;
    options.EmergencyMode.TriggerThreshold = 10000;  // 10k requests trigger emergency
    options.EmergencyMode.BlockDurationHours = 24;   // 24-hour emergency blocks
    options.EmergencyMode.SendAlerts = true;
    
    // Adaptive Rate Limiting
    options.EnableAdaptiveLimiting = true;
    options.ServerLoadThreshold = 0.7;       // 70% CPU threshold
    
    // Domain-based Rate Limiting
    options.EnableDomainRateLimiting = true;
    options.DomainRequestsPerMinute = 5000;  // 5000 requests per domain per minute
    options.DomainBurstCapacity = 10000;     // 10000 domain burst capacity
    
    // Storage Configuration
    options.StorageType = RateLimitStorageType.Redis;
    options.RedisConnectionString = "localhost:6379";
    options.RedisKeyPrefix = "myapp:rate_limit:";
    
    // Key Configuration
    options.KeyPrefix = "api";
});
```

---

## Per-Endpoint Rate Limiting

### Different Limits for Different Endpoints

```csharp
// Program.cs
var app = builder.Build();

// Public API - Lower limits
app.UseRateLimiting("/api/public", options =>
{
    options.Limit = 100;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 200;
    options.KeyPrefix = "public";
});

// Premium API - Higher limits
app.UseRateLimiting("/api/premium", options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 2000;
    options.KeyPrefix = "premium";
});

// Admin API - Very high limits
app.UseRateLimiting("/api/admin", options =>
{
    options.Limit = 10000;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 20000;
    options.KeyPrefix = "admin";
});

app.MapControllers();
```

### User-Based Rate Limiting

```csharp
// Custom middleware for user-based rate limiting
public class UserRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly RateLimitOptions _options;

    public UserRateLimitMiddleware(RequestDelegate next, IRateLimitService rateLimitService, IOptions<RateLimitOptions> options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get user ID from JWT token or session
        var userId = GetUserId(context);
        if (userId != null)
        {
            var userOptions = new RateLimitOptions
            {
                Limit = _options.Limit,
                WindowInSeconds = _options.WindowInSeconds,
                KeyPrefix = $"user:{userId}",
                Algorithm = RateLimitAlgorithm.TokenBucket,
                BurstCapacity = _options.BurstCapacity
            };

            var result = await _rateLimitService.CheckRateLimitAsync(context, userOptions);
            if (!result.IsAllowed)
            {
                context.Response.StatusCode = result.StatusCode;
                await context.Response.WriteAsync(result.ErrorMessage);
                return;
            }
        }

        await _next(context);
    }

    private string? GetUserId(HttpContext context)
    {
        // Extract user ID from JWT token
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (token != null)
        {
            // Decode JWT and extract user ID
            // Implementation depends on your JWT library
        }
        return null;
    }
}

// Usage
app.UseMiddleware<UserRateLimitMiddleware>();
```

---

## DDoS Protection

### Basic DDoS Protection

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    
    // Enable DDoS protection
    options.EnableDDoSProtection = true;
    options.DDoSThreshold = 5000;            // 5000 requests per minute = DDoS
    options.DDoSBlockMinutes = 60;           // Block for 1 hour
    options.ProgressiveBlocking = true;       // Escalate block duration
});
```

### Advanced DDoS Protection with Emergency Mode

```csharp
builder.Services.AddRateLimiting(options =>
{
    // Normal rate limiting
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    
    // DDoS Protection
    options.EnableDDoSProtection = true;
    options.DDoSThreshold = 5000;
    options.DDoSBlockMinutes = 60;
    options.ProgressiveBlocking = true;
    
    // Emergency Mode for severe attacks
    options.EmergencyMode.Enabled = true;
    options.EmergencyMode.TriggerThreshold = 20000;  // 20k requests = emergency
    options.EmergencyMode.BlockDurationHours = 24;   // 24-hour blocks
    options.EmergencyMode.SendAlerts = true;
});
```

### Custom DDoS Response

```csharp
public class CustomDDoSResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;

    public CustomDDoSResponseMiddleware(RequestDelegate next, IRateLimitService rateLimitService)
    {
        _next = next;
        _rateLimitService = rateLimitService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = new RateLimitOptions
        {
            Limit = 1000,
            WindowInSeconds = 60,
            EnableDDoSProtection = true,
            DDoSThreshold = 5000,
            DDoSBlockMinutes = 60
        };

        var result = await _rateLimitService.CheckRateLimitAsync(context, options);
        
        if (result.IsBlocked)
        {
            // Custom DDoS response
            context.Response.StatusCode = 429;
            context.Response.Headers.Add("Retry-After", result.RetryAfterSeconds.ToString());
            context.Response.Headers.Add("X-DDoS-Block-Reason", result.BlockReason);
            
            var response = new
            {
                error = "DDoS protection activated",
                reason = result.BlockReason,
                retryAfter = result.RetryAfterSeconds,
                resetTime = result.ResetTimeUtc
            };
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        await _next(context);
    }
}
```

---

## Adaptive Rate Limiting

### Server Load-Based Adaptation

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.BurstCapacity = 2000;
    
    // Enable adaptive rate limiting
    options.EnableAdaptiveLimiting = true;
    options.ServerLoadThreshold = 0.7;       // 70% CPU threshold
});
```

### Custom Load Monitoring

```csharp
public class CustomLoadMonitor
{
    private readonly ILogger<CustomLoadMonitor> _logger;
    private double _currentLoad = 0.0;

    public CustomLoadMonitor(ILogger<CustomLoadMonitor> logger)
    {
        _logger = logger;
    }

    public double GetCurrentLoad()
    {
        // Custom load calculation
        var process = Process.GetCurrentProcess();
        var cpuUsage = process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 1000.0;
        
        // Normalize to 0-1 range
        _currentLoad = Math.Min(1.0, cpuUsage);
        
        _logger.LogDebug("Current server load: {Load:F2}", _currentLoad);
        return _currentLoad;
    }
}

// Register custom load monitor
builder.Services.AddSingleton<CustomLoadMonitor>();

// Use in rate limiting configuration
builder.Services.AddRateLimiting(options =>
{
    options.EnableAdaptiveLimiting = true;
    options.ServerLoadThreshold = 0.6;
});
```

---

## Redis Distributed Storage

### Basic Redis Setup

```csharp
// Add Redis services
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

// Add rate limiting with Redis
builder.Services.AddRateLimitingWithRedisStorage(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.RedisConnectionString = "localhost:6379";
    options.RedisKeyPrefix = "myapp:rate_limit:";
});
```

### Redis with Connection Pooling

```csharp
// Configure Redis with connection pooling
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379");
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddRateLimitingWithRedisStorage(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.RedisConnectionString = "localhost:6379";
    options.RedisKeyPrefix = "myapp:rate_limit:";
});
```

### Redis Cluster Setup

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse("redis-cluster-node1:6379,redis-cluster-node2:6379,redis-cluster-node3:6379");
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddRateLimitingWithRedisStorage(options =>
{
    options.Limit = 1000;
    options.WindowInSeconds = 60;
    options.RedisKeyPrefix = "cluster:rate_limit:";
});
```

---

## Custom Storage Implementation

### Database Storage Implementation

```csharp
public class DatabaseRateLimitStore : IRateLimitStore
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DatabaseRateLimitStore> _logger;

    public DatabaseRateLimitStore(IDbConnection connection, ILogger<DatabaseRateLimitStore> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        var sql = "SELECT * FROM rate_limit_tokens WHERE key = @key";
        var bucket = await _connection.QueryFirstOrDefaultAsync<TokenBucket>(sql, new { key });
        return bucket;
    }

    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        var sql = @"
            INSERT INTO rate_limit_tokens (key, capacity, tokens, last_refill_time, refill_rate)
            VALUES (@Key, @Capacity, @Tokens, @LastRefillTime, @RefillRate)
            ON DUPLICATE KEY UPDATE
            capacity = @Capacity, tokens = @Tokens, last_refill_time = @LastRefillTime, refill_rate = @RefillRate";
        
        await _connection.ExecuteAsync(sql, bucket);
    }

    // Implement other methods...
}

// Register custom store
builder.Services.AddSingleton<IRateLimitStore, DatabaseRateLimitStore>();
```

### Hybrid Storage Implementation

```csharp
public class HybridRateLimitStore : IRateLimitStore
{
    private readonly IMemoryCache _memoryCache;
    private readonly IRateLimitStore _redisStore;
    private readonly ILogger<HybridRateLimitStore> _logger;

    public HybridRateLimitStore(IMemoryCache memoryCache, IRateLimitStore redisStore, ILogger<HybridRateLimitStore> logger)
    {
        _memoryCache = memoryCache;
        _redisStore = redisStore;
        _logger = logger;
    }

    public async Task<TokenBucket?> GetTokenBucketAsync(string key)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out TokenBucket? cachedBucket))
        {
            return cachedBucket;
        }

        // Fall back to Redis
        try
        {
            var bucket = await _redisStore.GetTokenBucketAsync(key);
            if (bucket != null)
            {
                // Cache in memory for 30 seconds
                _memoryCache.Set(key, bucket, TimeSpan.FromSeconds(30));
            }
            return bucket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get token bucket from Redis for key {Key}", key);
            return null;
        }
    }

    public async Task SetTokenBucketAsync(TokenBucket bucket)
    {
        // Update memory cache
        _memoryCache.Set(bucket.Key, bucket, TimeSpan.FromSeconds(30));

        // Update Redis
        try
        {
            await _redisStore.SetTokenBucketAsync(bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set token bucket in Redis for key {Key}", bucket.Key);
        }
    }

    // Implement other methods...
}
```

---

## Testing Examples

### Unit Testing

```csharp
[TestFixture]
public class RateLimitServiceTests
{
    private Mock<IRateLimitStore> _mockStore;
    private Mock<ILogger<AdvancedRateLimitService>> _mockLogger;
    private Mock<IMemoryCache> _mockCache;
    private AdvancedRateLimitService _service;

    [SetUp]
    public void Setup()
    {
        _mockStore = new Mock<IRateLimitStore>();
        _mockLogger = new Mock<ILogger<AdvancedRateLimitService>>();
        _mockCache = new Mock<IMemoryCache>();
        _service = new AdvancedRateLimitService(_mockStore.Object, _mockLogger.Object, _mockCache.Object);
    }

    [Test]
    public async Task CheckRateLimitAsync_WithinLimit_ShouldAllow()
    {
        // Arrange
        var context = CreateHttpContext();
        var options = new RateLimitOptions { Limit = 10, WindowInSeconds = 60 };
        
        _mockStore.Setup(x => x.GetTokenBucketAsync(It.IsAny<string>()))
                  .ReturnsAsync((TokenBucket?)null);

        // Act
        var result = await _service.CheckRateLimitAsync(context, options);

        // Assert
        Assert.IsTrue(result.IsAllowed);
        Assert.AreEqual(1, result.CurrentCount);
    }

    [Test]
    public async Task CheckRateLimitAsync_ExceedsLimit_ShouldBlock()
    {
        // Arrange
        var context = CreateHttpContext();
        var options = new RateLimitOptions { Limit = 1, WindowInSeconds = 60 };
        
        var existingBucket = new TokenBucket
        {
            Key = "test_key",
            Capacity = 1,
            Tokens = 0,
            LastRefillTime = DateTime.UtcNow,
            RefillRate = 1.0 / 60.0
        };
        
        _mockStore.Setup(x => x.GetTokenBucketAsync(It.IsAny<string>()))
                  .ReturnsAsync(existingBucket);

        // Act
        var result = await _service.CheckRateLimitAsync(context, options);

        // Assert
        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(0, result.RemainingRequests);
    }

    private HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return context;
    }
}
```

### Integration Testing

```csharp
[TestFixture]
public class RateLimitIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddRateLimiting(options =>
                    {
                        options.Limit = 5;
                        options.WindowInSeconds = 60;
                        options.StorageType = RateLimitStorageType.Memory;
                    });
                });
            });
        
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task RateLimitMiddleware_ShouldBlockAfterLimit()
    {
        // Arrange
        var requests = new List<Task<HttpResponseMessage>>();

        // Act - Make requests up to limit
        for (int i = 0; i < 5; i++)
        {
            requests.Add(_client.GetAsync("/api/test"));
        }

        var responses = await Task.WhenAll(requests);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        // Act - Make one more request
        var blockedResponse = await _client.GetAsync("/api/test");

        // Assert - Should be blocked
        Assert.AreEqual(HttpStatusCode.TooManyRequests, blockedResponse.StatusCode);
        
        // Check rate limit headers
        Assert.IsTrue(blockedResponse.Headers.Contains("X-RateLimit-Limit"));
        Assert.IsTrue(blockedResponse.Headers.Contains("Retry-After"));
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
```

### Load Testing

```csharp
[TestFixture]
public class RateLimitLoadTests
{
    [Test]
    public async Task RateLimitService_ShouldHandleHighConcurrency()
    {
        // Arrange
        var service = new AdvancedRateLimitService(store, logger, cache);
        var options = new RateLimitOptions { Limit = 1000, WindowInSeconds = 60 };
        var tasks = new List<Task<RateLimitResult>>();

        // Act - Simulate 1000 concurrent requests
        for (int i = 0; i < 1000; i++)
        {
            var context = CreateHttpContext($"192.168.1.{i % 255}");
            tasks.Add(service.CheckRateLimitAsync(context, options));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var allowedCount = results.Count(r => r.IsAllowed);
        var blockedCount = results.Count(r => !r.IsAllowed);
        
        Assert.AreEqual(1000, allowedCount); // All should be allowed within limit
        Assert.AreEqual(0, blockedCount);
    }
}
```

---

## Production Deployment

### Docker Configuration

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY . .

# Install Redis client
RUN apt-get update && apt-get install -y redis-tools

EXPOSE 80
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

```yaml
# docker-compose.yml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "80:80"
    environment:
      - RedisConnectionString=redis:6379
    depends_on:
      - redis
  
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data

volumes:
  redis_data:
```

### Kubernetes Configuration

```yaml
# k8s-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  replicas: 3
  selector:
    matchLabels:
      app: myapp
  template:
    metadata:
      labels:
        app: myapp
    spec:
      containers:
      - name: myapp
        image: myapp:latest
        ports:
        - containerPort: 80
        env:
        - name: RedisConnectionString
          value: "redis-service:6379"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: redis-service
spec:
  selector:
    app: redis
  ports:
  - port: 6379
    targetPort: 6379
```

### Environment Configuration

```json
// appsettings.Production.json
{
  "RateLimiting": {
    "Limit": 1000,
    "WindowInSeconds": 60,
    "BurstCapacity": 2000,
    "EnableDDoSProtection": true,
    "DDoSThreshold": 5000,
    "DDoSBlockMinutes": 60,
    "ProgressiveBlocking": true,
    "EnableAdaptiveLimiting": true,
    "ServerLoadThreshold": 0.7,
    "EmergencyMode": {
      "Enabled": true,
      "TriggerThreshold": 10000,
      "BlockDurationHours": 24,
      "SendAlerts": true
    },
    "StorageType": "Redis",
    "RedisConnectionString": "${REDIS_CONNECTION_STRING}",
    "RedisKeyPrefix": "prod:rate_limit:"
  }
}
```

---

## Troubleshooting Examples

### Debug Configuration

```csharp
// Enable debug logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Add rate limiting with debug options
builder.Services.AddRateLimiting(options =>
{
    options.Limit = 100;
    options.WindowInSeconds = 60;
    options.IncludeHeaders = true; // Include debug headers
});
```

### Health Check Implementation

```csharp
public class RateLimitHealthCheck : IHealthCheck
{
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<RateLimitHealthCheck> _logger;

    public RateLimitHealthCheck(IRateLimitService rateLimitService, ILogger<RateLimitHealthCheck> logger)
    {
        _rateLimitService = rateLimitService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var testContext = CreateTestHttpContext();
            var options = new RateLimitOptions { Limit = 1, WindowInSeconds = 60 };
            
            var result = await _rateLimitService.CheckRateLimitAsync(testContext, options);
            
            if (result.IsAllowed)
            {
                return HealthCheckResult.Healthy("Rate limiting service is working");
            }
            else
            {
                return HealthCheckResult.Unhealthy("Rate limiting service is blocking test requests");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limiting health check failed");
            return HealthCheckResult.Unhealthy("Rate limiting service is not responding", ex);
        }
    }

    private HttpContext CreateTestHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return context;
    }
}

// Register health check
builder.Services.AddHealthChecks()
    .AddCheck<RateLimitHealthCheck>("rate_limiting");
```

### Performance Monitoring

```csharp
public class RateLimitMetrics
{
    private readonly Counter _requestsTotal;
    private readonly Counter _blocksTotal;
    private readonly Counter _ddosBlocksTotal;
    private readonly Gauge _activeConnections;

    public RateLimitMetrics()
    {
        _requestsTotal = Metrics.CreateCounter("rate_limit_requests_total", "Total rate limit requests");
        _blocksTotal = Metrics.CreateCounter("rate_limit_blocks_total", "Total rate limit blocks");
        _ddosBlocksTotal = Metrics.CreateCounter("rate_limit_ddos_blocks_total", "Total DDoS blocks");
        _activeConnections = Metrics.CreateGauge("rate_limit_active_connections", "Active rate limit connections");
    }

    public void RecordRequest(bool isAllowed, bool isBlocked)
    {
        _requestsTotal.Inc();
        
        if (!isAllowed)
        {
            _blocksTotal.Inc();
        }
        
        if (isBlocked)
        {
            _ddosBlocksTotal.Inc();
        }
    }

    public void SetActiveConnections(int count)
    {
        _activeConnections.Set(count);
    }
}

// Register metrics
builder.Services.AddSingleton<RateLimitMetrics>();
```

### Custom Error Handling

```csharp
public class RateLimitErrorHandler
{
    private readonly ILogger<RateLimitErrorHandler> _logger;
    private readonly IWebHostEnvironment _environment;

    public RateLimitErrorHandler(ILogger<RateLimitErrorHandler> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task HandleRateLimitErrorAsync(HttpContext context, RateLimitResult result)
    {
        _logger.LogWarning("Rate limit exceeded for {ClientIp}: {Reason}", 
            GetClientIp(context), result.BlockReason);

        context.Response.StatusCode = result.StatusCode;
        context.Response.Headers.Add("Retry-After", result.RetryAfterSeconds.ToString());
        context.Response.Headers.Add("X-RateLimit-Limit", result.Limit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", result.RemainingRequests.ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", result.ResetTimeUtc.ToString("R"));

        var errorResponse = new
        {
            error = "Rate limit exceeded",
            message = result.ErrorMessage,
            retryAfter = result.RetryAfterSeconds,
            resetTime = result.ResetTimeUtc,
            limit = result.Limit,
            remaining = result.RemainingRequests
        };

        if (_environment.IsDevelopment())
        {
            errorResponse = new
            {
                error = "Rate limit exceeded",
                message = result.ErrorMessage,
                retryAfter = result.RetryAfterSeconds,
                resetTime = result.ResetTimeUtc,
                limit = result.Limit,
                remaining = result.RemainingRequests,
                debug = new
                {
                    key = result.Key,
                    blockReason = result.BlockReason,
                    isBlocked = result.IsBlocked
                }
            };
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    private string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

---

## Best Practices

### Configuration Management

```csharp
// Use strongly-typed configuration
public class RateLimitConfiguration
{
    public int Limit { get; set; } = 100;
    public int WindowInSeconds { get; set; } = 60;
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
    public string Algorithm { get; set; } = "TokenBucket";
    public string StorageType { get; set; } = "Memory";
    public string? RedisConnectionString { get; set; }
    public string RedisKeyPrefix { get; set; } = "rate_limit:";
}

// Register configuration
builder.Services.Configure<RateLimitConfiguration>(builder.Configuration.GetSection("RateLimiting"));

// Use in service registration
builder.Services.AddRateLimiting(options =>
{
    var config = builder.Configuration.GetSection("RateLimiting").Get<RateLimitConfiguration>()!;
    
    options.Limit = config.Limit;
    options.WindowInSeconds = config.WindowInSeconds;
    options.BurstCapacity = config.BurstCapacity;
    options.EnableDDoSProtection = config.EnableDDoSProtection;
    options.DDoSThreshold = config.DDoSThreshold;
    options.DDoSBlockMinutes = config.DDoSBlockMinutes;
    options.ProgressiveBlocking = config.ProgressiveBlocking;
    options.EnableAdaptiveLimiting = config.EnableAdaptiveLimiting;
    options.ServerLoadThreshold = config.ServerLoadThreshold;
    options.EmergencyMode = config.EmergencyMode;
    options.EnableDomainRateLimiting = config.EnableDomainRateLimiting;
    options.DomainRequestsPerMinute = config.DomainRequestsPerMinute;
    options.DomainBurstCapacity = config.DomainBurstCapacity;
    options.Algorithm = Enum.Parse<RateLimitAlgorithm>(config.Algorithm);
    options.StorageType = Enum.Parse<RateLimitStorageType>(config.StorageType);
    options.RedisConnectionString = config.RedisConnectionString;
    options.RedisKeyPrefix = config.RedisKeyPrefix;
});
```

### Security Considerations

```csharp
// Secure Redis connection
builder.Services.AddRateLimiting(options =>
{
    options.StorageType = RateLimitStorageType.Redis;
    options.RedisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
    options.RedisKeyPrefix = $"{Environment.GetEnvironmentVariable("APP_NAME")}:rate_limit:";
});

// IP whitelist for admin endpoints
public class AdminRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly IConfiguration _configuration;

    public AdminRateLimitMiddleware(RequestDelegate next, IRateLimitService rateLimitService, IConfiguration configuration)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = GetClientIpAddress(context);
        var whitelist = _configuration.GetSection("AdminIPWhitelist").Get<string[]>() ?? Array.Empty<string>();

        if (whitelist.Contains(clientIp))
        {
            // Skip rate limiting for whitelisted IPs
            await _next(context);
            return;
        }

        var options = new RateLimitOptions
        {
            Limit = 1000,
            WindowInSeconds = 60,
            KeyPrefix = "admin"
        };

        var result = await _rateLimitService.CheckRateLimitAsync(context, options);
        if (!result.IsAllowed)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Admin rate limit exceeded");
            return;
        }

        await _next(context);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
```

This comprehensive examples document provides practical implementations for all major use cases of the OElite.Restme.RateLimiting package, from basic setup to advanced production deployments.
