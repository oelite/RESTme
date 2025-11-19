# OElite.Restme.OpenSearch

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.OpenSearch.svg)](https://www.nuget.org/packages/OElite.Restme.OpenSearch)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

OpenSearch integration package for the Restme framework, providing search and analytics operations with simplified indexing and querying.

## Overview

OElite.Restme.OpenSearch provides powerful OpenSearch integration for the OElite platform, enabling full-text search, analytics, and document operations. Built with simplicity in mind, it abstracts away OpenSearch's complexity while providing access to its full search capabilities.

## Features

- **Document Operations**: Index, search, update, and delete documents
- **Full-Text Search**: Powerful text search with field-specific queries
- **Bulk Operations**: High-throughput bulk indexing and operations
- **Aggregation Engine**: Analytics and reporting with aggregations
- **Index Management**: Create and manage indices programmatically
- **Type-Safe Operations**: Strong typing with generic methods
- **Automatic Serialization**: JSON serialization for complex documents

## Installation

```bash
dotnet add package OElite.Restme.OpenSearch
```

## Quick Start

### Basic Configuration

```csharp
using OElite;

// Configure OpenSearch connection
var rest = new Rest("opensearch://localhost:9200", new RestConfig
{
    OperationMode = RestMode.OpenSearch
});
```

### Define Data Models

```csharp
// Define your searchable data model
public class Product
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public string[] Tags { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Index Documents

```csharp
// Index a single document
await rest.IndexAsync(new Product
{
    Id = "123",
    Name = "Wireless Headphones",
    Description = "High-quality wireless headphones with noise cancellation",
    Price = 199.99m,
    Category = "Electronics",
    Tags = new[] { "audio", "wireless", "noise-cancelling" },
    CreatedAt = DateTime.UtcNow
}, "products");

// Bulk index multiple documents
var products = LoadProductsFromDatabase();
await rest.IndexAsync(products, "products");
```

### Search Documents

```csharp
// Full-text search
var searchResults = await rest.SearchAsync<Product>(
    "wireless headphones", // search text
    new[] { "name", "description" }, // fields to search
    "products" // index name
);

// Advanced search with queries
var expensiveElectronics = await rest.SearchAsync<Product>(
    new SearchQuery {
        Query = "category:electronics AND price:[100 TO *]",
        PageSize = 50,
        SortFields = new[] { "price:desc" }
    }, "products"
);

// Get document by ID
var product = await rest.GetAsync<Product>("123", "products");
```

### Aggregation Analytics

```csharp
// Group and aggregate data
var categoryStats = await rest.AggregateAsync<Product>(
    p => p.Category,  // group by
    p => new { Count = p.Count(), AvgPrice = p.Average(x => x.Price) }, // aggregate
    "products"
);

// Access aggregation results
foreach (var bucket in categoryStats.Aggregations)
{
    Console.WriteLine($"{bucket.Key}: {bucket.Value}");
}
```

## Advanced Features

### Index Management

```csharp
// Create index with custom mapping
await rest.CreateIndexAsync<Product>("products", new OpenSearchMapping
{
    Properties = new Dictionary<string, object>
    {
        ["name"] = new { type = "text", analyzer = "standard" },
        ["price"] = new { type = "float" },
        ["tags"] = new { type = "keyword" }
    }
});

// List available indices
var indices = await rest.ListIndicesAsync();
```

### Complex Search Queries

```csharp
// Boolean queries
var complexQuery = new SearchQuery
{
    Query = "(wireless OR bluetooth) AND price:[50 TO 200] AND -category:accessories",
    Filters = new Dictionary<string, object>
    {
        ["created_at"] = new { gte = "2024-01-01" }
    },
    PageSize = 20,
    PageIndex = 0
};

var results = await rest.SearchAsync<Product>(complexQuery, "products");
```

### Delete Operations

```csharp
// Delete by ID
await rest.DeleteAsync("123", "products");

// Delete by query (advanced - requires careful use)
await rest.DeleteByQueryAsync<Product>(
    p => p.CreatedAt < DateTime.UtcNow.AddYears(-1),
    "products"
);
```

## Configuration Options

### Connection Strings

```csharp
// Basic connection
"opensearch://localhost:9200"

// With authentication
"opensearch://username:password@localhost:9200"

// Multiple nodes
"opensearch://node1:9200,node2:9200,node3:9200"

// Full configuration
"opensearch://localhost:9200?default_index=myapp&timeout=30000"
```

### Search Query Options

```csharp
var query = new SearchQuery
{
    Query = "search terms",
    PageSize = 50,
    PageIndex = 2,
    SortFields = new[] { "price:asc", "name:desc" },
    Filters = new Dictionary<string, object>
    {
        ["category"] = "electronics",
        ["price"] = new { gte = 100, lte = 1000 }
    }
};
```

## Best Practices

### Index Design
- Use appropriate analyzers for text fields
- Consider field types for optimal search performance
- Use nested objects for complex relationships
- Plan for index growth and sharding

### Search Optimization
- Use filters for structured data, queries for text
- Leverage aggregations for analytics
- Consider pagination for large result sets
- Use appropriate analyzers for your use case

### Performance Tips
- Use bulk operations for high-throughput indexing
- Configure appropriate refresh intervals
- Monitor search performance and optimize queries
- Use index aliases for zero-downtime reindexing

## Requirements

- **.NET 8.0, 9.0, or 10.0**
- **OpenSearch** (1.0+ recommended) or **Elasticsearch** (7.10+)
- **OElite.Restme** (dependency for base abstractions)

## Thread Safety

OpenSearchProvider is thread-safe for concurrent operations. Multiple threads can safely execute search, index, and other operations simultaneously.

## Error Handling

```csharp
try
{
    await rest.IndexAsync(product, "products");
}
catch (OpenSearchException ex)
{
    // Handle OpenSearch-specific errors
    _logger.LogError(ex, "Failed to index product");
    await RetryIndexAsync(product);
}
catch (OEliteException ex)
{
    // Handle Restme framework errors
    _logger.LogError(ex, "Restme operation failed");
}
```

## License

Copyright © Phanes Technology Ltd. All rights reserved.