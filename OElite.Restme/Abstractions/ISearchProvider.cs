using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Interface for OpenSearch operations
/// Provides simplified access to search, indexing, and analytics capabilities
/// </summary>
public interface ISearchProvider : IRestmeProvider
{
    /// <summary>
    /// Index a single document in OpenSearch
    /// </summary>
    Task IndexAsync<T>(T document, string indexName = null, string documentId = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Bulk index multiple documents in OpenSearch
    /// </summary>
    Task BulkIndexAsync<T>(IEnumerable<T> documents, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Get a document by ID from OpenSearch
    /// </summary>
    Task<T> GetAsync<T>(string documentId, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Search documents using a search query
    /// </summary>
    Task<SearchResult<T>> SearchAsync<T>(SearchQuery query, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Perform full-text search across specified fields
    /// </summary>
    Task<SearchResult<T>> TextSearchAsync<T>(string searchText, string[] fields = null, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Delete a document by ID
    /// </summary>
    Task<bool> DeleteAsync(string documentId, string indexName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete documents matching a query
    /// </summary>
    Task<bool> DeleteByQueryAsync<T>(Expression<Func<T, bool>> query, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Execute aggregation queries for analytics
    /// </summary>
    Task<AggregationResult> AggregateAsync<T>(AggregationQuery query, string indexName = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Create an OpenSearch index with automatic mapping
    /// </summary>
    Task CreateIndexAsync<T>(string indexName = null, OpenSearchMapping mapping = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// List all available indices
    /// </summary>
    Task<List<string>> ListIndicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Set TTL (Time To Live) for documents in an index
    /// </summary>
    Task SetIndexTTLAsync(string indexName, string ttlField, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure index lifecycle policy for automatic cleanup
    /// </summary>
    Task SetIndexLifecyclePolicyAsync(string indexName, TimeSpan deleteAfter, CancellationToken cancellationToken = default);
}