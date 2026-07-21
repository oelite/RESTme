# Kafka Connection String Format for OElite.Restme

## Overview

OElite.Restme.Kafka supports flexible Kafka connection string formats compatible with the unified `Rest` API.

## Connection String Formats

### **Format 1: URI Format (Recommended)**
```
kafka://host:port
```

**Examples:**
```csharp
// Single broker
"kafka://localhost:9092"

// Production broker
"kafka://kafka.production.com:9092"

// Multiple brokers (comma-separated)
"kafka://kafka1:9092,kafka2:9092,kafka3:9092"
```

### **Format 2: Plain Bootstrap Servers**
```
host:port
```

**Examples:**
```csharp
// Single broker (kafka:// prefix is optional)
"localhost:9092"

// Multiple brokers
"kafka1:9092,kafka2:9092,kafka3:9092"
```

## Authentication

### **SASL/PLAIN Authentication**

Use `RestConfig.AuthKey` and `RestConfig.AuthSecret` for username/password authentication:

```csharp
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = "kafka://kafka.production.com:9092",
    AuthKey = "your-username",      // SASL username
    AuthSecret = "your-password"     // SASL password
});

var kafka = rest.GetProvider<IEventStreamProvider>();
```

**Supported Mechanisms:**
- **SASL/PLAIN** - Username/password authentication
- **Security Protocol**: `SASL_PLAINTEXT` (automatically configured when credentials provided)

### **No Authentication (Development)**

```csharp
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = "kafka://localhost:9092"
    // No AuthKey/AuthSecret = no authentication
});
```

## Complete Usage Examples

### **Example 1: Local Development (No Auth)**

```csharp
using OElite.Restme;
using OElite.Restme.Abstractions;

// Simple local setup
var rest = new Rest("kafka://localhost:9092", RestMode.Kafka);
var kafka = rest.GetProvider<IEventStreamProvider>();

// Publish event
await kafka.PublishAsync(new OrderCreatedEvent 
{ 
    OrderId = "12345",
    Amount = 99.99m 
}, "orders");

// Subscribe to events
await kafka.SubscribeAsync<OrderCreatedEvent>("orders", "order-processor", async evt =>
{
    Console.WriteLine($"Processing order: {evt.OrderId}");
});
```

### **Example 2: Production with Authentication**

```csharp
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = "kafka://kafka1.prod.com:9092,kafka2.prod.com:9092,kafka3.prod.com:9092",
    AuthKey = Environment.GetEnvironmentVariable("KAFKA_USERNAME"),
    AuthSecret = Environment.GetEnvironmentVariable("KAFKA_PASSWORD")
});

var kafka = rest.GetProvider<IEventStreamProvider>();

// Publish batch events
var events = new[]
{
    new UserRegisteredEvent { UserId = "user1", Email = "user1@example.com" },
    new UserRegisteredEvent { UserId = "user2", Email = "user2@example.com" }
};

await kafka.PublishBatchAsync(events, "user-events", e => e.UserId);
```

### **Example 3: ASP.NET Core Integration**

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register Kafka from configuration
var kafkaConnectionString = builder.Configuration.GetConnectionString("Kafka"); // "kafka://kafka:9092"
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = kafkaConnectionString,
    AuthKey = builder.Configuration["Kafka:Username"],
    AuthSecret = builder.Configuration["Kafka:Password"]
});

builder.Services.AddSingleton(rest.GetProvider<IEventStreamProvider>());

var app = builder.Build();

// Use in controllers/services
public class OrderService
{
    private readonly IEventStreamProvider _kafka;

    public OrderService(IEventStreamProvider kafka)
    {
        _kafka = kafka;
    }

    public async Task CreateOrderAsync(Order order)
    {
        // Business logic
        await _repository.SaveAsync(order);

        // Publish event
        await _kafka.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Amount = order.Total
        }, "orders", key: order.CustomerId);
    }
}
```

### **Example 4: Configuration File (appsettings.json)**

```json
{
  "ConnectionStrings": {
    "Kafka": "kafka://kafka1:9092,kafka2:9092"
  },
  "Kafka": {
    "Username": "api-user",
    "Password": "secret-password",
    "Topics": {
      "Orders": "orders",
      "Users": "user-events",
      "Notifications": "notifications"
    }
  }
}
```

```csharp
// Startup configuration
var kafkaConfig = new RestConfig(RestMode.Kafka)
{
    ConnectionString = builder.Configuration.GetConnectionString("Kafka"),
    AuthKey = builder.Configuration["Kafka:Username"],
    AuthSecret = builder.Configuration["Kafka:Password"]
};

var rest = new Rest(kafkaConfig);
builder.Services.AddSingleton(rest.GetProvider<IEventStreamProvider>());
```

## Connection String Parsing

The `KafkaConnection` class parses connection strings as follows:

```csharp
// Input: "kafka://host1:9092,host2:9092"
// Parsed: "host1:9092,host2:9092" (kafka:// prefix removed)

// Input: "localhost:9092"
// Parsed: "localhost:9092" (used as-is)
```

The parsed value becomes the `BootstrapServers` configuration for Confluent.Kafka.

## Kafka Configuration Details

### **Producer Configuration (Optimized)**

```csharp
BootstrapServers = _bootstrapServers
Acks = Acks.All                    // Wait for all replicas
EnableIdempotence = true           // Prevent duplicates
MaxInFlight = 5                    // Pipeline up to 5 requests
MessageTimeoutMs = 30000           // 30 second timeout
```

### **Consumer Configuration**

```csharp
BootstrapServers = _bootstrapServers
GroupId = consumerGroup            // Set per subscription
AutoOffsetReset = AutoOffsetReset.Earliest  // Start from beginning
EnableAutoCommit = true            // Auto-commit offsets
SessionTimeoutMs = 30000           // 30 second session timeout
HeartbeatIntervalMs = 3000         // 3 second heartbeat
```

### **SASL/PLAIN Security (When Auth Provided)**

```csharp
SecurityProtocol = SecurityProtocol.SaslPlaintext
SaslMechanism = SaslMechanism.Plain
SaslUsername = config.AuthKey
SaslPassword = config.AuthSecret
```

## Common Use Cases

### **Event Sourcing**

```csharp
var kafka = rest.GetProvider<IEventStreamProvider>();

// Store events in event log
await kafka.PublishAsync(new AccountDebited { AccountId = "A1", Amount = 100 }, "account-events");
await kafka.PublishAsync(new AccountCredited { AccountId = "A1", Amount = 50 }, "account-events");

// Replay events to rebuild state
await kafka.SubscribeAsync<object>("account-events", "state-builder", async evt =>
{
    // Rebuild account state from events
});
```

### **Change Data Capture (CDC)**

```csharp
// Publish database changes as events
await kafka.PublishAsync(new UserUpdated 
{ 
    UserId = user.Id, 
    Email = user.Email,
    UpdatedAt = DateTime.UtcNow 
}, "user-changes");

// Sync to read replicas
await kafka.SubscribeAsync<UserUpdated>("user-changes", "read-replica-sync", async evt =>
{
    await _readReplica.UpdateUserAsync(evt);
});
```

### **Microservices Event Bus**

```csharp
// Service A: Publish domain events
await kafka.PublishAsync(new PaymentProcessed 
{ 
    PaymentId = "P123",
    OrderId = "O456" 
}, "payments");

// Service B: React to events
await kafka.SubscribeAsync<PaymentProcessed>("payments", "order-fulfillment", async evt =>
{
    await _orderService.FulfillOrderAsync(evt.OrderId);
});
```

## Environment-Specific Configuration

### **Development (Docker Compose)**

```yaml
services:
  kafka:
    image: confluentinc/cp-kafka:latest
    ports:
      - "9092:9092"
    environment:
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
```

```csharp
// Connection string
"kafka://localhost:9092"
```

### **Staging/Production (Confluent Cloud)**

```csharp
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = "kafka://pkc-xxxxx.us-east-1.aws.confluent.cloud:9092",
    AuthKey = "API_KEY",
    AuthSecret = "API_SECRET"
});
```

### **Production (Self-Hosted Cluster)**

```csharp
var rest = new Rest(new RestConfig(RestMode.Kafka)
{
    ConnectionString = "kafka://kafka1.internal:9092,kafka2.internal:9092,kafka3.internal:9092",
    AuthKey = "cluster-user",
    AuthSecret = "cluster-password"
});
```

## Troubleshooting

### **Connection Failed**

```
Error: Failed to connect to Kafka broker
```

**Solutions:**
- Verify broker is running: `docker ps` or check cluster health
- Check connection string format: `kafka://host:port`
- Verify network connectivity: `telnet kafka-host 9092`
- Check firewall rules allow port 9092

### **Authentication Failed**

```
Error: SASL authentication failed
```

**Solutions:**
- Verify `AuthKey` and `AuthSecret` are correct
- Check SASL mechanism is PLAIN (only mechanism currently supported)
- Verify broker has SASL/PLAIN listener enabled

### **Topic Not Found**

```
Error: Topic 'my-topic' does not exist
```

**Solutions:**
- Create topic before publishing: `await kafka.CreateTopicAsync("my-topic")`
- Enable auto-create topics in broker configuration
- Check topic name spelling

## Best Practices

1. **Use URI Format**: `kafka://host:port` for clarity
2. **Multiple Brokers**: Always configure multiple brokers for production: `kafka://broker1:9092,broker2:9092,broker3:9092`
3. **Environment Variables**: Store credentials in environment variables, never hardcode
4. **Connection Reuse**: Create `Rest` instance once and reuse (connection pooling handled internally)
5. **Topic Naming**: Use descriptive names: `orders`, `user-events`, `payment-notifications`
6. **Consumer Groups**: Use meaningful consumer group names for monitoring: `order-processor`, `email-sender`
7. **Partition Keys**: Provide partition keys for ordering: `await kafka.PublishAsync(event, "orders", key: customerId)`

## Compatibility

- **Confluent.Kafka**: OElite.Restme.Kafka uses Confluent.Kafka client library
- **Kafka Versions**: Compatible with Apache Kafka 2.x, 3.x
- **Protocols**: SASL/PLAIN (other mechanisms can be added via custom configuration)

## See Also

- [IEventStreamProvider API Documentation](./API_DOCUMENTATION.md)
- [Kafka Integration Tests](../tests/OElite.Restme.Kafka.IntegrationTests/)
- [OElite.Restme Core Documentation](../OElite.Restme/README.md)
