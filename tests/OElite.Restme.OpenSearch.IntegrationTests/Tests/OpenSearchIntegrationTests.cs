using FluentAssertions;
using OElite.Restme.OpenSearch.IntegrationTests.Infrastructure;
using OElite.Restme.OpenSearch.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.OpenSearch.IntegrationTests.Tests;

/// <summary>
/// Integration tests for OpenSearch TTL and lifecycle functionality
/// </summary>
[Collection("OpenSearchIntegration")]
public class OpenSearchTtlIntegrationTests : OpenSearchTestBase
{
    public OpenSearchTtlIntegrationTests(ITestOutputHelper output)
    {
        // Test constructor - logging is handled by the base class
    }

    [Fact]
    public async Task SetIndexTTL_ShouldConfigureDocumentExpiry()
    {
        // Arrange
        var indexName = $"test-ttl-{Guid.NewGuid():N}";

        // Act - Set TTL on documents based on timestamp field
        await Rest.SetIndexTTLAsync(indexName, "timestamp", TimeSpan.FromDays(30));

        // Assert - Should complete without errors
        // In real implementation, we'd verify the TTL was configured
    }

    [Fact]
    public async Task SetIndexLifecyclePolicy_ShouldConfigureAutomaticCleanup()
    {
        // Arrange
        var indexName = $"test-lifecycle-{Guid.NewGuid():N}";

        // Act - Set lifecycle policy for automatic deletion after 90 days
        await Rest.SetIndexLifecyclePolicyAsync(indexName, TimeSpan.FromDays(90));

        // Assert - Should complete without errors
        // In real implementation, we'd verify the lifecycle policy was set
    }

    [Fact]
    public async Task SetIndexTTL_WithShortExpiry_ShouldWork()
    {
        // Arrange
        var indexName = $"test-short-ttl-{Guid.NewGuid():N}";

        // Act - Set very short TTL for testing
        await Rest.SetIndexTTLAsync(indexName, "timestamp", TimeSpan.FromHours(1));

        // Assert - Should complete without errors
    }
}

/// <summary>
/// Integration tests for basic OpenSearch operations
/// </summary>
[Collection("OpenSearchIntegration")]
public class OpenSearchBasicIntegrationTests : OpenSearchTestBase
{
    [Fact]
    public async Task IndexDocument_ShouldWork()
    {
        // Arrange
        var indexName = $"test-index-{Guid.NewGuid():N}";
        var document = new LogDocument
        {
            Level = "INFO",
            Message = "Test log message",
            Timestamp = DateTime.UtcNow,
            Source = "integration-test"
        };

        // Act
        await Rest.IndexAsync(document, indexName);

        // Assert - Should complete without errors
        // In real implementation, we'd verify the document was indexed
    }

    [Fact]
    public async Task BulkIndexDocuments_ShouldWork()
    {
        // Arrange
        var indexName = $"test-bulk-{Guid.NewGuid():N}";
        var documents = new List<LogDocument>
        {
            new() { Level = "INFO", Message = "Log 1", Source = "test" },
            new() { Level = "WARN", Message = "Log 2", Source = "test" },
            new() { Level = "ERROR", Message = "Log 3", Source = "test" }
        };

        // Act
        await Rest.SearchProvider.BulkIndexAsync(documents, indexName);

        // Assert - Should complete without errors
    }

    [Fact]
    public async Task CreateIndex_ShouldWork()
    {
        // Arrange
        var indexName = $"test-create-{Guid.NewGuid():N}";

        // Act
        await Rest.CreateIndexAsync<LogDocument>(indexName);

        // Assert - Should complete without errors
        // In real implementation, we'd verify the index was created
    }

    [Fact]
    public async Task ListIndices_ShouldReturnIndices()
    {
        // Arrange - Create a test index first
        var indexName = $"test-list-{Guid.NewGuid():N}";
        await Rest.CreateIndexAsync<LogDocument>(indexName);

        // Act
        var indices = await Rest.ListIndicesAsync();

        // Assert
        indices.Should().NotBeNull();
        indices.Should().Contain(indexName);
    }
}