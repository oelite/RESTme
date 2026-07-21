using System;
using System.Collections.Generic;

namespace OElite.Restme.RabbitMQ.IntegrationTests.Models;

/// <summary>
/// Test message model for RabbitMQ integration tests
/// Designed for comprehensive queue operation validation
/// </summary>
public class QueueMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
    public int Priority { get; set; } = 0;
    public Dictionary<string, string> Headers { get; set; } = new();

    public override string ToString()
    {
        return $"QueueMessage[{Id}]: {Content} (Priority: {Priority})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is QueueMessage other)
        {
            return Id == other.Id &&
                   Content == other.Content &&
                   Priority == other.Priority &&
                   Source == other.Source;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Content, Priority, Source);
    }
}

/// <summary>
/// Order processing message for testing workflow scenarios
/// </summary>
public class OrderMessage
{
    public Guid OrderId { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public override string ToString()
    {
        return $"Order[{OrderId}]: {Status} - ${Amount:F2}";
    }
}

/// <summary>
/// Order status enumeration for workflow testing
/// </summary>
public enum OrderStatus
{
    Created = 0,
    Validated = 1,
    Paid = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

/// <summary>
/// User activity message for testing event-driven scenarios
/// </summary>
public class UserActivityMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();

    public override string ToString()
    {
        return $"UserActivity[{UserId}]: {Action} at {Timestamp:yyyy-MM-dd HH:mm:ss}";
    }
}

/// <summary>
/// Error testing message that can be configured to cause serialization issues
/// </summary>
public class ErrorTestMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Data { get; set; } = string.Empty;
    public bool CauseSerializationError { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Property that will cause serialization issues if enabled
    public object? ProblematicProperty => CauseSerializationError ?
        new { CircularRef = this } : null;
}

/// <summary>
/// Performance test message for load testing scenarios
/// </summary>
public class PerformanceTestMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SequenceNumber { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public TimeSpan? ProcessingDuration => ProcessedAt - CreatedAt;

    /// <summary>
    /// Creates a message with a payload of specified size (for performance testing)
    /// </summary>
    public static PerformanceTestMessage CreateWithPayloadSize(int sequenceNumber, int payloadSizeInBytes)
    {
        var payload = new string('A', Math.Max(0, payloadSizeInBytes));
        return new PerformanceTestMessage
        {
            SequenceNumber = sequenceNumber,
            Payload = payload
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }

    public override string ToString()
    {
        var duration = ProcessingDuration?.TotalMilliseconds.ToString("F2") ?? "N/A";
        return $"PerfTest[{SequenceNumber}]: {Payload.Length} bytes, {duration}ms";
    }
}

/// <summary>
/// Batch processing message for testing bulk operations
/// </summary>
public class BatchProcessingMessage
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public int BatchSize { get; set; }
    public int ItemIndex { get; set; }
    public string Data { get; set; } = string.Empty;
    public DateTime BatchStartTime { get; set; } = DateTime.UtcNow;
    public bool IsLastInBatch => ItemIndex == BatchSize - 1;

    public override string ToString()
    {
        return $"Batch[{BatchId}] Item {ItemIndex + 1}/{BatchSize}: {Data}";
    }
}