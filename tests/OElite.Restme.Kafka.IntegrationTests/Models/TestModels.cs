using OElite;

namespace OElite.Restme.Kafka.IntegrationTests.Models;

/// <summary>
/// Test message for Kafka integration tests
/// </summary>
public class UserActivityMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }
}

/// <summary>
/// Test message for order events
/// </summary>
public class OrderEventMessage
{
    public Guid OrderId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "created", "paid", "shipped", "delivered"
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}