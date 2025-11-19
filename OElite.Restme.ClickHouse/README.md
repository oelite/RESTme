# OElite.Restme.ClickHouse

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.ClickHouse.svg)](https://www.nuget.org/packages/OElite.Restme.ClickHouse)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

ClickHouse integration package for the Restme framework, providing columnar database operations with LINQ expression support for time-series analytics and high-performance data operations.

## Overview

OElite.Restme.ClickHouse provides powerful ClickHouse integration for the OElite platform, enabling high-performance analytics, time-series data processing, and complex aggregations. Built with LINQ expression support, it translates C# queries to ClickHouse SQL automatically, making analytics accessible without SQL knowledge.

## Features

- **LINQ Expression Support**: Write C# queries that automatically translate to ClickHouse SQL
- **Time-Series Analytics**: Built-in time-series query helpers with automatic date filtering
- **High-Performance Operations**: Optimized for ClickHouse's columnar architecture
- **Automatic Schema Inference**: Create tables from C# classes automatically
- **Bulk Operations**: Efficient bulk insert operations for high-throughput data loading
- **Aggregation Engine**: Powerful aggregation queries for analytics and reporting
- **Type-Safe Queries**: Compile-time query validation with LINQ expressions
- **TTL Support**: Table-level TTL expressions and automatic data expiration

## Installation

```bash
dotnet add package OElite.Restme.ClickHouse
```

## Quick Start

### Basic Configuration

```csharp
using OElite;

// Configure ClickHouse connection
var rest = new Rest("clickhouse://localhost:8123", new RestConfig
{
    OperationMode = RestMode.ClickHouse
});
```

### Define Data Models

```csharp
// Define your data model with optional table attribute
[ClickHouseTable("user_events")]
public class UserEvent
{
    public string UserId { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

// Or use automatic table naming (user_event)
public class ProductView
{
    public string ProductId { get; set; }
    public string UserId { get; set; }
    public DateTime ViewedAt { get; set; }
    public TimeSpan ViewDuration { get; set; }
    public string Source { get; set; }
}
```

### Create Tables

```csharp
// Create table with automatic schema inference
await rest.CreateTableAsync<UserEvent>(engine: ClickHouseEngine.MergeTree);

// Or specify custom table name
await rest.CreateTableAsync<ProductView>("product_views", ClickHouseEngine.MergeTree);
```

### Insert Data

```csharp
// Single insert
var userEvent = new UserEvent
{
    UserId = "user123",
    EventType = "page_view",
    Timestamp = DateTime.UtcNow,
    SessionId = "sess_456",
    Metadata = new Dictionary<string, object>
    {
        ["page"] = "/products",
        ["user_agent"] = "Chrome/91.0"
    }
};

await rest.InsertAsync(userEvent);

// Bulk insert for high performance
var events = GenerateUserEvents(1000);
await rest.BulkInsertAsync(events);
```

### Query with LINQ Expressions

```csharp
// LINQ expressions automatically translate to ClickHouse SQL
var recentEvents = await rest.QueryAsync<UserEvent>(
    e => e.Timestamp > DateTime.UtcNow.AddHours(-1) &&
         e.EventType == "page_view");

// Complex queries with string operations
var searchEvents = await rest.QueryAsync<UserEvent>(
    e => e.UserId.Contains("user") &&
         e.Metadata.ContainsKey("page"));

// Range queries
var activeUsers = await rest.QueryAsync<UserEvent>(
    e => e.Timestamp >= startDate && e.Timestamp < endDate &&
         e.EventType == "login");
```

### Time-Series Queries

```csharp
// Automatic time-series analysis
var hourlyStats = await rest.TimeSeriesAsync<UserEvent>(
    "user_events",
    DateTime.UtcNow.AddDays(-7),  // Start date
    DateTime.UtcNow,              // End date
    "hour"                        // Group by hour
);

// Access results
foreach (var series in hourlyStats.Series)
{
    Console.WriteLine($"Hour: {series.Key}, Events: {series.Value.Count}");
}
```

### Aggregation Queries

```csharp
// Powerful aggregations
var userStats = await rest.AggregateAsync(
    "user_events",
    "SELECT UserId, count() as EventCount, uniq(EventType) as UniqueEvents " +
    "GROUP BY UserId ORDER BY EventCount DESC LIMIT 10"
);

// Access aggregation results
foreach (var result in userStats.Aggregations)
{
    Console.WriteLine($"{result.Key}: {result.Value}");
}
```

### TTL (Time To Live) Configuration

ClickHouse supports automatic data expiration through TTL expressions:

```csharp
// Set TTL on table data (data older than 30 days will be automatically deleted)
await rest.SetTableTTLAsync("user_events", "created_at + INTERVAL 30 DAY");

// Create TTL index for automatic cleanup (combines TTL with indexing)
await rest.CreateTTLIndexAsync("logs", "timestamp", TimeSpan.FromDays(90));

// Advanced TTL expressions
await rest.SetTableTTLAsync("metrics", "timestamp + INTERVAL 1 YEAR TO VOLUME 'cold'");
await rest.SetTableTTLAsync("sessions", "last_access + INTERVAL 24 HOUR TO DISK 'ssd'");
```

**TTL Expression Syntax:**
- `column_name + INTERVAL n UNIT` - Delete data older than n units from column
- `TO VOLUME 'volume_name'` - Move data to specific volume instead of deleting
- `TO DISK 'disk_name'` - Move data to specific disk instead of deleting
- Multiple TTL rules can be combined with different destinations

### Raw SQL Queries

```csharp
// Direct SQL when you need full control
var complexQuery = @"
    SELECT
        UserId,
        EventType,
        count() as EventCount,
        topK(10)(Metadata['page']) as TopPages
    FROM user_events
    WHERE Timestamp >= @start AND Timestamp < @end
    GROUP BY UserId, EventType
    ORDER BY EventCount DESC
    LIMIT 100";

var results = await rest.QueryAsync<UserAnalytics>(complexQuery, new
{
    start = DateTime.UtcNow.AddDays(-30),
    end = DateTime.UtcNow
});
```

## Advanced Features

### Custom Table Engines

```csharp
// Different ClickHouse engines for different use cases
await rest.CreateTableAsync<AuditLog>("audit_logs",
    ClickHouseEngine.ReplacingMergeTree);  // For deduplication

await rest.CreateTableAsync<MetricsData>("metrics",
    ClickHouseEngine.SummingMergeTree);   // For incremental sums

await rest.CreateTableAsync<SessionData>("sessions",
    ClickHouseEngine.CollapsingMergeTree); // For state changes
```

### Complex LINQ Queries

```csharp
// Nested conditions and method calls
var filteredEvents = await rest.QueryAsync<UserEvent>(e =>
    e.Timestamp.Date == DateTime.Today &&
    (e.EventType == "login" || e.EventType == "signup") &&
    e.UserId.StartsWith("user") &&
    e.Metadata.ContainsKey("source") &&
    e.Metadata["source"].ToString() == "mobile");

// Array operations (when using array columns)
var usersWithTags = await rest.QueryAsync<UserProfile>(u =>
    u.Tags.Contains("premium") && u.Tags.Length > 2);
```

### Performance Optimization

```csharp
// Count operations are optimized
var totalEvents = await rest.CountAsync("user_events");
var todaysEvents = await rest.CountAsync("user_events",
    "Timestamp >= today() AND Timestamp < tomorrow()");

// Efficient existence checks
var hasData = await rest.CountAsync("user_events") > 0;
```

## LINQ Expression Support

Restme.ClickHouse supports a comprehensive set of LINQ expressions:

### Comparison Operators
```csharp
// Equality, inequality
e => e.Status == "active"
e => e.Count != 0

// Greater/Less than
e => e.Timestamp > DateTime.UtcNow.AddHours(-1)
e => e.Score >= 100
e => e.Amount < 1000
e => e.Rating <= 5
```

### Logical Operators
```csharp
// AND, OR, NOT
e => e.Status == "active" && e.Type == "premium"
e => e.Category == "A" || e.Category == "B"
e => !(e.Deleted == true)
```

### String Operations
```csharp
// String methods
e => e.Name.Contains("test")
e => e.Email.StartsWith("admin")
e => e.Code.EndsWith("123")
e => e.Description.Length > 10
```

### Collection Operations
```csharp
// Array operations
e => e.Tags.Contains("important")
e => e.Categories.Length > 2
e => e.Scores.Contains(100)
```

### Date/Time Operations
```csharp
// Date comparisons
e => e.CreatedAt.Date == DateTime.Today
e => e.CreatedAt.Year == 2024
e => e.CreatedAt.Month == 1
```

## Configuration Options

### Connection Strings

```csharp
// Basic connection
"clickhouse://localhost:8123"

// With authentication
"clickhouse://username:password@localhost:8123"

// With database
"clickhouse://localhost:8123/database_name"

// Full connection string
"clickhouse://user:pass@host:8123/db?compress=true&connection_timeout=10000"
```

### RestConfig Options

```csharp
var config = new RestConfig
{
    OperationMode = RestMode.ClickHouse,
    // ClickHouse-specific options can be added here
    UseCompression = true,
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    CommandTimeout = TimeSpan.FromMinutes(5)
};
```

## Best Practices

### Schema Design
- Use appropriate ClickHouse engines for your use case
- Design tables with time-series data in mind
- Use appropriate data types to minimize storage

### Query Optimization
- Prefer LINQ expressions for type safety
- Use time-series helpers for date-based queries
- Leverage ClickHouse's aggregation functions
- Consider partitioning for large datasets

### Performance Tips
- Use bulk inserts for high-throughput scenarios
- Prefer MergeTree engine for most use cases
- Use appropriate indexes (ORDER BY clauses)
- Monitor query performance and optimize as needed

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **ClickHouse Server** (21.0+ recommended)
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

ClickHouseProvider is thread-safe for concurrent operations. Multiple threads can safely execute queries, inserts, and other operations simultaneously.

## Error Handling

```csharp
try
{
    await rest.InsertAsync(userEvent);
}
catch (ClickHouseException ex)
{
    // Handle ClickHouse-specific errors
    _logger.LogError(ex, "Failed to insert user event");
}
catch (OEliteException ex)
{
    // Handle Restme framework errors
    _logger.LogError(ex, "Restme operation failed");
}
```

## License

Copyright © Phanes Technology Ltd. All rights reserved.