using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Abstractions
{
    /// <summary>
    /// Interface for ClickHouse columnar database operations
    /// Provides simplified access to ClickHouse analytics and time-series capabilities
    /// </summary>
    public interface IColumnarProvider : IDisposable
    {
        /// <summary>
        /// Insert a single data record into ClickHouse table
        /// </summary>
        Task InsertAsync<T>(T data, string tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk insert multiple records into ClickHouse table for high-performance data loading
        /// </summary>
        Task BulkInsertAsync<T>(IEnumerable<T> data, string tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a SQL query and return strongly-typed results
        /// </summary>
        Task<List<T>> QueryAsync<T>(string sql, object parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a LINQ expression query and return strongly-typed results
        /// Translates LINQ expressions to ClickHouse SQL automatically
        /// </summary>
        Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, string tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Count records in a table with optional WHERE clause
        /// </summary>
        Task<long> CountAsync(string tableName, string whereClause = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute time-series queries with automatic date range filtering and grouping
        /// </summary>
        Task<TimeSeriesResult<T>> TimeSeriesAsync<T>(string tableName, DateTime start, DateTime end, string groupBy = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute aggregation queries for analytics and reporting
        /// </summary>
        Task<AggregationResult> AggregateAsync(string tableName, string aggregationQuery, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a ClickHouse table with automatic schema inference from entity type
        /// </summary>
        Task CreateTableAsync<T>(string tableName = null, ClickHouseEngine engine = ClickHouseEngine.MergeTree, CancellationToken cancellationToken = default);

        /// <summary>
        /// Set TTL (Time To Live) configuration for a ClickHouse table
        /// </summary>
        Task SetTableTTLAsync(string tableName, string ttlExpression, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a TTL index for automatic record expiration
        /// </summary>
        Task CreateTTLIndexAsync(string tableName, string columnName, TimeSpan ttl, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for Kafka streaming operations
    /// Provides simplified access to Kafka topics, partitions, and consumer groups
    /// </summary>
    public interface IStreamingProvider : IDisposable
    {
        /// <summary>
        /// Publish a single message to a Kafka topic
        /// </summary>
        Task PublishAsync<T>(T message, string topicName, string key = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish multiple messages to a Kafka topic in batch
        /// </summary>
        Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to a Kafka topic with a message handler
        /// </summary>
        Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to topics matching a pattern with a message handler
        /// </summary>
        Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default);

        /// <summary>
        /// Process messages from a topic and return a stream result
        /// </summary>
        Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a Kafka topic with specified configuration
        /// </summary>
        Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// List all available Kafka topics
        /// </summary>
        Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Set retention policy for a Kafka topic (message expiry)
        /// </summary>
        Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configure message timestamp-based expiry for a topic
        /// </summary>
        Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for OpenSearch operations
    /// Provides simplified access to search, indexing, and analytics capabilities
    /// </summary>
    public interface ISearchProvider : IDisposable
    {
        /// <summary>
        /// Index a single document in OpenSearch
        /// </summary>
        Task IndexAsync<T>(T document, string indexName = null, string documentId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk index multiple documents in OpenSearch
        /// </summary>
        Task BulkIndexAsync<T>(IEnumerable<T> documents, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a document by ID from OpenSearch
        /// </summary>
        Task<T> GetAsync<T>(string documentId, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search documents using a search query
        /// </summary>
        Task<SearchResult<T>> SearchAsync<T>(SearchQuery query, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform full-text search across specified fields
        /// </summary>
        Task<SearchResult<T>> TextSearchAsync<T>(string searchText, string[] fields = null, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a document by ID
        /// </summary>
        Task<bool> DeleteAsync(string documentId, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete documents matching a query
        /// </summary>
        Task<bool> DeleteByQueryAsync<T>(Expression<Func<T, bool>> query, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute aggregation queries for analytics
        /// </summary>
        Task<AggregationResult> AggregateAsync<T>(AggregationQuery query, string indexName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create an OpenSearch index with automatic mapping
        /// </summary>
        Task CreateIndexAsync<T>(string indexName = null, OpenSearchMapping mapping = null, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// ClickHouse table engines
    /// </summary>
    public enum ClickHouseEngine
    {
        MergeTree,
        ReplacingMergeTree,
        SummingMergeTree,
        AggregatingMergeTree,
        CollapsingMergeTree,
        VersionedCollapsingMergeTree,
        GraphiteMergeTree
    }

    /// <summary>
    /// Result of time-series queries
    /// </summary>
    public class TimeSeriesResult<T>
    {
        public List<T> Data { get; set; } = new();
        public Dictionary<string, List<object>> Series { get; set; } = new();
        public TimeSpan QueryDuration { get; set; }
        public long TotalRecords { get; set; }
    }

    /// <summary>
    /// Result of aggregation queries
    /// </summary>
    public class AggregationResult
    {
        public Dictionary<string, object> Aggregations { get; set; } = new();
        public TimeSpan QueryDuration { get; set; }
        public long ProcessedRecords { get; set; }
    }

    /// <summary>
    /// Result of streaming operations
    /// </summary>
    public class StreamResult<T>
    {
        public IAsyncEnumerable<T> Messages { get; set; }
        public string ConsumerGroup { get; set; }
        public string TopicName { get; set; }
        public long MessagesProcessed { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }

    /// <summary>
    /// Search query for OpenSearch operations
    /// </summary>
    public class SearchQuery
    {
        public string Query { get; set; }
        public int PageSize { get; set; } = 20;
        public int PageIndex { get; set; } = 0;
        public string[] SortFields { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Filters { get; set; } = new();
    }

    /// <summary>
    /// Search result from OpenSearch operations
    /// </summary>
    public class SearchResult<T>
    {
        public List<T> Documents { get; set; } = new();
        public long TotalCount { get; set; }
        public TimeSpan QueryDuration { get; set; }
        public Dictionary<string, object> Aggregations { get; set; } = new();
        public int PageSize { get; set; }
        public int PageIndex { get; set; }
    }

    /// <summary>
    /// Aggregation query for analytics operations
    /// </summary>
    public class AggregationQuery
    {
        public string GroupBy { get; set; }
        public Dictionary<string, string> Aggregations { get; set; } = new();
        public Dictionary<string, object> Filters { get; set; } = new();
    }

    /// <summary>
    /// OpenSearch index mapping configuration
    /// </summary>
    public class OpenSearchMapping
    {
        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
    }
}