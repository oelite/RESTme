using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite
{
    /// <summary>
    /// OpenSearch extension methods for IRestme
    /// </summary>
    public static class OpenSearchRestmeExtensions
    {
        /// <summary>
        /// Index a single document in OpenSearch
        /// </summary>
        public static async Task IndexAsync<T>(this IRestme rest, T document,
            string indexName = null, string documentId = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            await rest.SearchProvider.IndexAsync(document, indexName, documentId, cancellationToken);
        }

        /// <summary>
        /// Bulk index multiple documents in OpenSearch
        /// </summary>
        public static async Task IndexAsync<T>(this IRestme rest, IEnumerable<T> documents,
            string indexName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            await rest.SearchProvider.BulkIndexAsync(documents, indexName, cancellationToken);
        }

        /// <summary>
        /// Search documents using a search query
        /// </summary>
        public static async Task<SearchResult<T>> SearchAsync<T>(this IRestme rest,
            SearchQuery query, string indexName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            return await rest.SearchProvider.SearchAsync<T>(query, indexName, cancellationToken);
        }

        /// <summary>
        /// Perform full-text search across specified fields
        /// </summary>
        public static async Task<SearchResult<T>> SearchAsync<T>(this IRestme rest,
            string searchText, string[] fields = null, string indexName = null,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            return await rest.SearchProvider.TextSearchAsync<T>(searchText, fields, indexName, cancellationToken);
        }

        /// <summary>
        /// Get a document by ID from OpenSearch
        /// </summary>
        public static async Task<T> GetAsync<T>(this IRestme rest, string documentId,
            string indexName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            return await rest.SearchProvider.GetAsync<T>(documentId, indexName, cancellationToken);
        }

        /// <summary>
        /// Execute aggregation queries for analytics
        /// </summary>
        public static async Task<AggregationResult> AggregateAsync<T>(this IRestme rest,
            AggregationQuery query, string indexName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            return await rest.SearchProvider.AggregateAsync<T>(query, indexName, cancellationToken);
        }

        /// <summary>
        /// Create an OpenSearch index with automatic mapping
        /// </summary>
        public static async Task CreateIndexAsync<T>(this IRestme rest,
            string indexName = null, OpenSearchMapping mapping = null,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            await rest.SearchProvider.CreateIndexAsync<T>(indexName, mapping, cancellationToken);
        }

        /// <summary>
        /// List all available indices
        /// </summary>
        public static async Task<List<string>> ListIndicesAsync(this IRestme rest,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            return await rest.SearchProvider.ListIndicesAsync(cancellationToken);
        }

        /// <summary>
        /// Set TTL (Time To Live) for documents in an index
        /// </summary>
        public static async Task SetIndexTTLAsync(this IRestme rest, string indexName,
            string ttlField, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            await rest.SearchProvider.SetIndexTTLAsync(indexName, ttlField, ttl, cancellationToken);
        }

        /// <summary>
        /// Configure index lifecycle policy for automatic cleanup
        /// </summary>
        public static async Task SetIndexLifecyclePolicyAsync(this IRestme rest, string indexName,
            TimeSpan deleteAfter, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.OpenSearch)
                throw new OEliteException("OpenSearch mode required");

            if (rest.SearchProvider == null)
                throw new OEliteException("OpenSearch provider not initialized");

            await rest.SearchProvider.SetIndexLifecyclePolicyAsync(indexName, deleteAfter, cancellationToken);
        }
    }
}