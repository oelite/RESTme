# OElite.Restme.RabbitMQ

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.RabbitMQ.svg)](https://www.nuget.org/packages/OElite.Restme.RabbitMQ)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

RabbitMQ integration package for the Restme framework, providing enterprise-grade message queuing and asynchronous communication capabilities.

## Overview

OElite.Restme.RabbitMQ provides robust RabbitMQ integration for the OElite platform, enabling reliable message queuing, publish-subscribe patterns, and distributed communication. Built on the official RabbitMQ .NET client, it offers enterprise-grade reliability with automatic reconnection, message persistence, and flexible routing capabilities.

## Features

- **Reliable Message Queuing**: Enterprise-grade message queuing with delivery guarantees
- **Publish-Subscribe Pattern**: Support for topic-based messaging and event broadcasting
- **Automatic Serialization**: JSON serialization/deserialization for complex message types
- **Connection Management**: Robust connection handling with automatic reconnection
- **Virtual Host Support**: Multi-tenancy through virtual host isolation
- **Flexible Routing**: Exchange-based routing with custom routing keys
- **Error Handling**: Comprehensive error handling with dead letter queues
- **Async/Await Support**: Full asynchronous operations for optimal performance
- **Message Persistence**: Durable queues and persistent messages for reliability

## Installation

```bash
dotnet add package OElite.Restme.RabbitMQ
```

## Quick Start

### Basic Configuration

```csharp
using OElite.Providers;

// Basic RabbitMQ connection
var connectionString = "localhost:5672";
var config = new RestConfig();
var queueProvider = new RabbitMQProvider(connectionString, config);

// With authentication
var authConnection = "amqp://username:password@localhost:5672";
var authProvider = new RabbitMQProvider(authConnection, config);

// With virtual host
var vhostConnection = "amqp://user:pass@host:5672|vhost=production";
var vhostProvider = new RabbitMQProvider(vhostConnection, config);
```

### Basic Queue Operations

```csharp
// Send message to queue
await queueProvider.SendAsync("order.processing", orderData);

// Receive message from queue
var message = await queueProvider.ReceiveAsync<OrderData>("order.processing");

// Send with routing key
await queueProvider.SendAsync("notifications", emailData, "email.urgent");

// Subscribe to queue with message handler
await queueProvider.SubscribeAsync<NotificationData>("notifications", HandleNotification);

private async Task HandleNotification(NotificationData notification)
{
    // Process notification
    await ProcessNotificationAsync(notification);
}
```

## Core Features

### Message Queuing

Reliable point-to-point messaging:

```csharp
// Define message types
public class OrderProcessingMessage
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
}

// Send order processing message
var message = new OrderProcessingMessage
{
    OrderId = "ORD-123",
    CustomerId = "CUST-456",
    Amount = 99.99m,
    OrderDate = DateTime.UtcNow
};

await queueProvider.SendAsync("order.processing", message);

// Process orders
await queueProvider.SubscribeAsync<OrderProcessingMessage>("order.processing", async (order) =>
{
    await ProcessOrderAsync(order);
});
```

### Publish-Subscribe Pattern

Event-driven messaging:

```csharp
// Publisher: Send events to multiple subscribers
public class EventPublisher
{
    private readonly IQueueProvider _queue;

    public EventPublisher(IQueueProvider queue)
    {
        _queue = queue;
    }

    public async Task PublishUserRegisteredAsync(UserRegisteredEvent evt)
    {
        await _queue.SendAsync("user.events", evt, "user.registered");
    }

    public async Task PublishOrderPlacedAsync(OrderPlacedEvent evt)
    {
        await _queue.SendAsync("order.events", evt, "order.placed");
    }
}

// Subscribers: Handle specific events
public class EmailNotificationService
{
    public async Task StartAsync(IQueueProvider queue)
    {
        await queue.SubscribeAsync<UserRegisteredEvent>("user.events.email",
            HandleUserRegistered, "user.registered");
    }

    private async Task HandleUserRegistered(UserRegisteredEvent evt)
    {
        await SendWelcomeEmailAsync(evt.UserId, evt.Email);
    }
}

public class AnalyticsService
{
    public async Task StartAsync(IQueueProvider queue)
    {
        await queue.SubscribeAsync<OrderPlacedEvent>("order.events.analytics",
            HandleOrderPlaced, "order.placed");
    }

    private async Task HandleOrderPlaced(OrderPlacedEvent evt)
    {
        await RecordOrderMetricsAsync(evt);
    }
}
```

### Request-Response Pattern

Synchronous-style communication over async messaging:

```csharp
// Request processing service
public class OrderService
{
    private readonly IQueueProvider _queue;

    public OrderService(IQueueProvider queue)
    {
        _queue = queue;
    }

    public async Task StartRequestHandlerAsync()
    {
        await _queue.SubscribeAsync<OrderValidationRequest>("order.validation.requests",
            HandleValidationRequest);
    }

    private async Task HandleValidationRequest(OrderValidationRequest request)
    {
        var response = new OrderValidationResponse
        {
            RequestId = request.RequestId,
            IsValid = await ValidateOrderAsync(request.Order),
            ValidationErrors = GetValidationErrors(request.Order)
        };

        await _queue.SendAsync("order.validation.responses", response, request.RequestId);
    }
}

// Client making requests
public class OrderValidator
{
    private readonly IQueueProvider _queue;
    private readonly Dictionary<string, TaskCompletionSource<OrderValidationResponse>> _pendingRequests;

    public OrderValidator(IQueueProvider queue)
    {
        _queue = queue;
        _pendingRequests = new Dictionary<string, TaskCompletionSource<OrderValidationResponse>>();

        // Start listening for responses
        StartResponseListenerAsync();
    }

    public async Task<OrderValidationResponse> ValidateOrderAsync(Order order)
    {
        var requestId = Guid.NewGuid().ToString();
        var request = new OrderValidationRequest
        {
            RequestId = requestId,
            Order = order
        };

        var tcs = new TaskCompletionSource<OrderValidationResponse>();
        _pendingRequests[requestId] = tcs;

        await _queue.SendAsync("order.validation.requests", request);

        // Wait for response with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => tcs.TrySetCanceled());

        return await tcs.Task;
    }

    private async Task StartResponseListenerAsync()
    {
        await _queue.SubscribeAsync<OrderValidationResponse>("order.validation.responses",
            HandleValidationResponse);
    }

    private Task HandleValidationResponse(OrderValidationResponse response)
    {
        if (_pendingRequests.TryGetValue(response.RequestId, out var tcs))
        {
            _pendingRequests.Remove(response.RequestId);
            tcs.SetResult(response);
        }
        return Task.CompletedTask;
    }
}
```

## Advanced Configuration

### Connection String Options

```csharp
// Basic format
"localhost:5672"
"rabbitmq.example.com:5672"

// With AMQP URI
"amqp://guest:guest@localhost:5672"
"amqps://user:pass@secure-rabbit.com:5671"

// With virtual host
"amqp://user:pass@host:5672|vhost=production"
"localhost:5672|vhost=development"

// SSL connection
"amqps://user:pass@secure-rabbit.com:5671|vhost=prod"
```

### Queue Configuration

```csharp
// Configure queue properties
public class QueueConfig
{
    public bool Durable { get; set; } = true;          // Survive server restart
    public bool Exclusive { get; set; } = false;       // Queue exclusive to connection
    public bool AutoDelete { get; set; } = false;      // Delete when unused
    public Dictionary<string, object> Arguments { get; set; } = new();
}

// Create queue with configuration
var config = new QueueConfig
{
    Durable = true,
    Arguments = new Dictionary<string, object>
    {
        ["x-message-ttl"] = 3600000,  // 1 hour TTL
        ["x-max-length"] = 10000,     // Maximum queue length
        ["x-dead-letter-exchange"] = "dlx.exchange"
    }
};
```

### Exchange Configuration

```csharp
// Configure exchanges for routing
public class ExchangeConfig
{
    public string Name { get; set; }
    public string Type { get; set; } = "topic";  // direct, fanout, topic, headers
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public Dictionary<string, object> Arguments { get; set; } = new();
}

// Topic exchange for flexible routing
var exchangeConfig = new ExchangeConfig
{
    Name = "application.events",
    Type = "topic",
    Durable = true
};
```

## Integration Patterns

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IQueueProvider>(provider =>
{
    var connectionString = configuration.GetConnectionString("RabbitMQ");
    var config = new RestConfig();
    return new RabbitMQProvider(connectionString, config);
});

// Background service for message processing
public class MessageProcessorService : BackgroundService
{
    private readonly IQueueProvider _queue;
    private readonly ILogger<MessageProcessorService> _logger;

    public MessageProcessorService(IQueueProvider queue, ILogger<MessageProcessorService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _queue.SubscribeAsync<OrderMessage>("order.processing", ProcessOrder);

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessOrder(OrderMessage message)
    {
        try
        {
            await ProcessOrderAsync(message);
            _logger.LogInformation("Processed order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order {OrderId}", message.OrderId);
            throw;
        }
    }
}
```

### Event-Driven Architecture

```csharp
// Domain events
public class DomainEventPublisher
{
    private readonly IQueueProvider _queue;

    public DomainEventPublisher(IQueueProvider queue)
    {
        _queue = queue;
    }

    public async Task PublishAsync<T>(T domainEvent, string routingKey = "") where T : IDomainEvent
    {
        await _queue.SendAsync("domain.events", domainEvent, routingKey);
    }
}

// Event handlers
public class UserEventHandler
{
    private readonly IQueueProvider _queue;
    private readonly IUserService _userService;

    public UserEventHandler(IQueueProvider queue, IUserService userService)
    {
        _queue = queue;
        _userService = userService;
    }

    public async Task StartAsync()
    {
        await _queue.SubscribeAsync<UserCreatedEvent>("user.events.handler",
            HandleUserCreated, "user.created");

        await _queue.SubscribeAsync<UserUpdatedEvent>("user.events.handler",
            HandleUserUpdated, "user.updated");
    }

    private async Task HandleUserCreated(UserCreatedEvent evt)
    {
        await _userService.InitializeUserProfileAsync(evt.UserId);
        await _userService.SendWelcomeEmailAsync(evt.UserId);
    }

    private async Task HandleUserUpdated(UserUpdatedEvent evt)
    {
        await _userService.UpdateUserCacheAsync(evt.UserId);
    }
}
```

### Microservices Communication

```csharp
// Service-to-service messaging
public class PaymentService
{
    private readonly IQueueProvider _queue;

    public PaymentService(IQueueProvider queue)
    {
        _queue = queue;
    }

    public async Task ProcessPaymentAsync(PaymentRequest request)
    {
        try
        {
            // Process payment
            var result = await ProcessPaymentInternalAsync(request);

            if (result.Success)
            {
                await _queue.SendAsync("payment.events", new PaymentCompletedEvent
                {
                    PaymentId = request.PaymentId,
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    ProcessedAt = DateTime.UtcNow
                }, "payment.completed");
            }
            else
            {
                await _queue.SendAsync("payment.events", new PaymentFailedEvent
                {
                    PaymentId = request.PaymentId,
                    OrderId = request.OrderId,
                    Reason = result.FailureReason,
                    FailedAt = DateTime.UtcNow
                }, "payment.failed");
            }
        }
        catch (Exception ex)
        {
            await _queue.SendAsync("payment.events", new PaymentErrorEvent
            {
                PaymentId = request.PaymentId,
                ErrorMessage = ex.Message,
                ErrorAt = DateTime.UtcNow
            }, "payment.error");

            throw;
        }
    }
}

// Order service listening for payment events
public class OrderService
{
    public async Task StartAsync(IQueueProvider queue)
    {
        await queue.SubscribeAsync<PaymentCompletedEvent>("order.payment.events",
            HandlePaymentCompleted, "payment.completed");

        await queue.SubscribeAsync<PaymentFailedEvent>("order.payment.events",
            HandlePaymentFailed, "payment.failed");
    }

    private async Task HandlePaymentCompleted(PaymentCompletedEvent evt)
    {
        await UpdateOrderStatusAsync(evt.OrderId, OrderStatus.Paid);
        await TriggerOrderFulfillmentAsync(evt.OrderId);
    }

    private async Task HandlePaymentFailed(PaymentFailedEvent evt)
    {
        await UpdateOrderStatusAsync(evt.OrderId, OrderStatus.PaymentFailed);
        await NotifyCustomerAsync(evt.OrderId, evt.Reason);
    }
}
```

## Error Handling and Reliability

### Dead Letter Queues

```csharp
// Configure dead letter exchange
var queueConfig = new QueueConfig
{
    Arguments = new Dictionary<string, object>
    {
        ["x-dead-letter-exchange"] = "dlx.failed.messages",
        ["x-dead-letter-routing-key"] = "failed",
        ["x-message-ttl"] = 300000  // 5 minutes before DLQ
    }
};

// Handle failed messages
await queueProvider.SubscribeAsync<FailedMessage>("dlx.failed.messages", async (failedMsg) =>
{
    _logger.LogError("Message failed processing: {Message}", failedMsg);
    await StoreFailedMessageAsync(failedMsg);
    await AlertOperationsTeamAsync(failedMsg);
});
```

### Retry Logic

```csharp
public class ReliableMessageHandler
{
    private readonly IQueueProvider _queue;
    private readonly ILogger _logger;

    public async Task HandleMessageWithRetryAsync<T>(T message, Func<T, Task> handler, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await handler(message);
                return; // Success
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Message processing failed, attempt {Attempt}/{MaxRetries}",
                    attempt, maxRetries);

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message processing failed after {MaxRetries} attempts", maxRetries);
                throw;
            }
        }
    }
}
```

## Performance Considerations

### Connection Pooling

```csharp
// Single provider instance for connection reuse
var provider = new RabbitMQProvider(connectionString, config);

// All operations use the same connection
await provider.SendAsync("queue1", message1);
await provider.SendAsync("queue2", message2);
await provider.ReceiveAsync<Message>("queue3");
```

### Batch Processing

```csharp
// Process messages in batches for efficiency
public async Task ProcessMessagesBatchAsync(string queueName, int batchSize = 10)
{
    var messages = new List<ProcessingMessage>();

    await _queue.SubscribeAsync<ProcessingMessage>(queueName, async (message) =>
    {
        messages.Add(message);

        if (messages.Count >= batchSize)
        {
            await ProcessBatchAsync(messages.ToList());
            messages.Clear();
        }
    });

    // Process remaining messages periodically
    using var timer = new Timer(async _ =>
    {
        if (messages.Count > 0)
        {
            await ProcessBatchAsync(messages.ToList());
            messages.Clear();
        }
    }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
}
```

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **RabbitMQ.Client 7.1.2+**
- **OElite.Restme** (dependency for base abstractions)
- **RabbitMQ Server** (3.8+ recommended)

## Thread Safety

RabbitMQProvider is thread-safe for most operations:

- Connection and channel management is thread-safe
- Send operations are thread-safe
- Subscribe operations should be set up once per queue
- Can be used as a singleton in DI containers

## License

Copyright © OElite Limited. All rights reserved.