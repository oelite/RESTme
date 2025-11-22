using OElite;

namespace OElite.Restme.OpenSearch.IntegrationTests.Models;

/// <summary>
/// Test document for OpenSearch integration tests
/// </summary>
public class LogDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Source { get; set; }
    public string? UserId { get; set; }
}

/// <summary>
/// Test document for user activity
/// </summary>
public class UserActivityDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Page { get; set; }
    public int Duration { get; set; }
}