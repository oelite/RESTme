using System.ComponentModel.DataAnnotations.Schema;
using OElite;

namespace OElite.Restme.ClickHouse.IntegrationTests.Models;

/// <summary>
/// Test entity for ClickHouse integration tests
/// </summary>
[Table("user_events")]
public class UserEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; }
    public int EventValue { get; set; }
}

/// <summary>
/// Test entity for TTL functionality
/// </summary>
[Table("logs")]
public class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
}