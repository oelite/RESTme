# OElite.Restme.Kafka

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.Kafka.svg)](https://www.nuget.org/packages/OElite.Restme.Kafka)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Kafka integration package for the Restme framework, providing streaming operations with simplified topic management and consumer group handling.

## Overview

OElite.Restme.Kafka provides powerful Kafka integration for the OElite platform, enabling high-performance streaming, pub/sub messaging, and real-time data processing. Built with simplicity in mind, it abstracts away Kafka's complexity while providing access to its full streaming capabilities.

## Features

- **Generic Provider Factory**: Auto-registered via `ServiceLocator` with Streaming capability
- **Provider Capability**: `ProviderCapabilities.Streaming` - Kafka provides streaming operations
- **Simple Publishing**: Stream messages to topics with automatic serialization
- **Batch Operations**: High-throughput batch publishing with custom partitioning
- **Consumer Groups**: Simplified consumer group management and load balancing
- **Pattern Subscription**: Subscribe to multiple topics using patterns
- **Stream Processing**: Real-time stream processing with async enumeration
- **Topic Management**: Create and manage topics programmatically
- **Retention Policies**: Configure topic retention and message expiry
- **Automatic Serialization**: JSON serialization for complex message types

## Installation

```bash
dotnet add package OElite.Restme.Kafka
```

## Quick Start

### Basic Configuration

```csharp
using OElite;
using OElite.Abstractions;

// Option 1: Using Rest with generic provider pattern (recommended)
var rest = new Rest("kafka://localhost:9092",
    configuration: new RestConfig
    {
        OperationMode = RestMode.Kafka,
        AuthKey = "kafka-user",        // Optional
        AuthSecret = "kafka-password"  // Optional
    });

// Get streaming provider using generic factory pattern
var streamingProvider = rest.GetProvider<IStreamingProvider>();

// NEW: Named providers for multiple Kafka clusters
var orderStreamProvider = rest.GetProvider<IStreamingProvider>("orders");
var analyticsStreamProvider = rest.GetProvider<IStreamingProvider>("analytics");
var loggingStreamProvider = rest.GetProvider<IStreamingProvider>("logging");

// Option 2: Legacy connection string (still supported)
var rest = new Rest("kafka://localhost:9092", new RestConfig
{
    OperationMode = RestMode.Kafka
});
```

### Publishing Messages

```csharp
// Single message publish
await rest.PublishAsync(new OrderEvent
{
    OrderId = "123",
    UserId = "user456",
    Amount = 99.99m,
    Status = "placed"
}, "order-events", "user456"); // key for partitioning

// Batch publish for high throughput
var events = GenerateOrderEvents(1000);
await rest.PublishAsync(events, "order-events",
    order => order.UserId); // custom partitioning
```

### Consuming Messages

```csharp
// Subscribe to a topic with consumer group
await rest.SubscribeAsync<OrderEvent>("order-events", "order-processor",
    async (order) => {
        await ProcessOrderAsync(order);
    });

// Subscribe to multiple topics with pattern
await rest.SubscribePatternAsync<OrderEvent>("order-*", "analytics",
    async (topicName, order) => {
        await AnalyzeOrderAsync(topicName, order);
    });
```

### Stream Processing

```csharp
// Process messages as a continuous stream
var orderStream = await rest.ProcessAsync<OrderEvent>("order-events", "realtime-analytics");

// Process messages in real-time
await foreach (var order in orderStream.Messages)
{
    await UpdateRealTimeMetricsAsync(order);
}
```

### Topic Management

```csharp
// Create topics programmatically
await rest.CreateTopicAsync("user-events", partitions: 3, replicationFactor: 2);

// List available topics
var topics = await rest.ListTopicsAsync();
```

## Advanced Features

### Custom Partitioning

```csharp
// Custom partitioning logic
await rest.PublishAsync(largeOrderBatch, "orders",
    order => order.Region); // partition by region

await rest.PublishAsync(userEvents, "user-activity",
    userEvent => userEvent.UserId.GetHashCode() % 10); // custom hash partitioning
```

### Consumer Group Management

```csharp
// Multiple consumer groups for different processing needs
await rest.SubscribeAsync<OrderEvent>("orders", "inventory-service", HandleInventory);
await rest.SubscribeAsync<OrderEvent>("orders", "notification-service", SendNotifications);
await rest.SubscribeAsync<OrderEvent>("orders", "analytics-service", UpdateAnalytics);
```

### Named Streaming Providers

**NEW in v2.1.0**: Support for multiple streaming provider instances using named providers:

```csharp
// Create Rest instance
var rest = new Rest("kafka://localhost:9092", new RestConfig { OperationMode = RestMode.Kafka });

// Get named streaming providers for different purposes
var orderStreaming = rest.GetProvider<IStreamingProvider>("orders");
var eventStreaming = rest.GetProvider<IStreamingProvider>("events");
var analyticsStreaming = rest.GetProvider<IStreamingProvider>("analytics");
var loggingStreaming = rest.GetProvider<IStreamingProvider>("logging");

// Use different streaming providers for logical separation
await orderStreaming.PublishAsync(orderEvent, "order-events", orderEvent.UserId);
await eventStreaming.PublishAsync(userEvent, "user-events", userEvent.UserId);
await analyticsStreaming.PublishAsync(analyticsEvent, "analytics-events");
await loggingStreaming.PublishAsync(logEvent, "application-logs");

// Default provider (backward compatible)
var defaultStreaming = rest.GetProvider<IStreamingProvider>(); // Same as GetProvider<IStreamingProvider>("default")

// Named providers enable clean streaming architecture
public class EventPublishingService
{
    private readonly IStreamingProvider _orderStreaming;
    private readonly IStreamingProvider _userStreaming;
    private readonly IStreamingProvider _systemStreaming;

    public EventPublishingService(Rest rest)
    {
        _orderStreaming = rest.GetProvider<IStreamingProvider>("orders");
        _userStreaming = rest.GetProvider<IStreamingProvider>("users");
        _systemStreaming = rest.GetProvider<IStreamingProvider>("system");
    }

    public async Task PublishOrderEventAsync(OrderEvent orderEvent)
    {
        await _orderStreaming.PublishAsync(orderEvent, "order-processing", orderEvent.OrderId);
    }

    public async Task PublishUserEventAsync(UserEvent userEvent)
    {
        await _userStreaming.PublishAsync(userEvent, "user-activity", userEvent.UserId);
    }

    public async Task PublishSystemEventAsync(SystemEvent systemEvent)
    {
        await _systemStreaming.PublishAsync(systemEvent, "system-monitoring", systemEvent.Source);
    }
}

// Multi-cluster streaming with named providers
public class MultiClusterEventService
{
    private readonly IStreamingProvider _primaryCluster;
    private readonly IStreamingProvider _analyticsCluster;
    private readonly IStreamingProvider _backupCluster;

    public MultiClusterEventService()
    {
        var primaryRest = new Rest("kafka://primary.cluster:9092", new RestConfig { OperationMode = RestMode.Kafka });
        var analyticsRest = new Rest("kafka://analytics.cluster:9092", new RestConfig { OperationMode = RestMode.Kafka });
        var backupRest = new Rest("kafka://backup.cluster:9092", new RestConfig { OperationMode = RestMode.Kafka });

        _primaryCluster = primaryRest.GetProvider<IStreamingProvider>("primary");
        _analyticsCluster = analyticsRest.GetProvider<IStreamingProvider>("analytics");
        _backupCluster = backupRest.GetProvider<IStreamingProvider>("backup");
    }

    public async Task PublishCriticalEventAsync<T>(T eventData, string topic) where T : class
    {
        // Publish to both primary and backup clusters
        var publishTasks = new[]
        {
            _primaryCluster.PublishAsync(eventData, topic),
            _backupCluster.PublishAsync(eventData, $"backup-{topic}")
        };

        await Task.WhenAll(publishTasks);

        // Also send to analytics cluster for processing
        await _analyticsCluster.PublishAsync(eventData, $"analytics-{topic}");
    }
}
```

### Error Handling and Resilience

```csharp
try
{
    await rest.PublishAsync(orderEvent, "orders");
}
catch (KafkaException ex)
{
    // Handle Kafka-specific errors
    _logger.LogError(ex, "Failed to publish order event");
    await RetryPublishAsync(orderEvent);
}
```

## Configuration Options

### Authentication Configuration

#### Using RestConfig (Recommended)
```csharp
var config = new RestConfig
{
    AuthKey = "kafka-user",        // SASL username (optional)
    AuthSecret = "kafka-password", // SASL password (optional)
    OperationMode = RestMode.Kafka
};
var rest = new Rest("kafka://localhost:9092", config);
```

#### Legacy Connection Strings (Still Supported)
```csharp
// Basic connection
"kafka://localhost:9092"

// Multiple brokers
"kafka://broker1:9092,broker2:9092,broker3:9092"

// With authentication
"kafka://username:password@broker1:9092,broker2:9092"

// Full configuration
"kafka://broker1:9092,broker2:9092?group.id=my-app&auto.offset.reset=earliest"
```

### Producer Configuration

```csharp
// High-throughput producer
await rest.PublishAsync(largeBatch, "high-volume-topic",
    keySelector: item => item.Id,
    compression: CompressionType.Gzip,
    batchSize: 1000);
```

### Consumer Configuration

```csharp
// Real-time consumer
await rest.SubscribeAsync<Event>("events", "realtime-consumer",
    handler: ProcessRealtime,
    autoCommit: false,
    maxPollRecords: 100);
```

## Best Practices

### Message Design
- Use descriptive topic names
- Include message keys for proper partitioning
- Design messages for evolution (backward compatibility)
- Keep messages focused and single-purpose

### Consumer Groups
- Use unique consumer group IDs for different applications
- Scale consumers within the same group for load balancing
- Use different groups for different processing logic

### Performance Optimization
- Use batch publishing for high-throughput scenarios
- Configure appropriate partition counts
- Monitor consumer lag and adjust as needed
- Use compression for large messages

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **Apache Kafka** (2.8+ recommended)
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

KafkaProvider is thread-safe for concurrent publishing operations. Consumer operations should be set up once per application.

## License

Copyright © Phanes Technology Ltd. All rights reserved.