using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using OElite.Restme.Abstractions;

namespace OElite.Restme.Base
{
    /// <summary>
    /// Default ClickHouse provider that throws helpful error messages
    /// </summary>
    public class DefaultColumnarProvider : IColumnarProvider
    {
        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "DefaultColumnar";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => new (RestMode.Memory);

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.Columnar;

        public Task InsertAsync<T>(T data, string tableName = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task BulkInsertAsync<T>(IEnumerable<T> data, string tableName = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task<List<T>> QueryAsync<T>(string sql, object parameters = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, string tableName = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task<long> CountAsync(string tableName, string whereClause = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task<TimeSeriesResult<T>> TimeSeriesAsync<T>(string tableName, DateTime start, DateTime end, string groupBy = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task<AggregationResult> AggregateAsync(string tableName, string aggregationQuery, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task CreateTableAsync<T>(string tableName = null, ClickHouseEngine engine = ClickHouseEngine.MergeTree, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task SetTableTTLAsync(string tableName, string ttlExpression, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public Task CreateTTLIndexAsync(string tableName, string columnName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("ClickHouse provider not available. Please install OElite.Restme.ClickHouse package and configure ClickHouse connection.");
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    /// <summary>
    /// Default Kafka provider that throws helpful error messages
    /// </summary>
    public class DefaultStreamingProvider : IEventStreamProvider
    {
        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "DefaultStreaming";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => new (RestMode.Memory);

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.EventStream;

        public Task PublishAsync<T>(T message, string topicName, string key = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task PublishBatchAsync<T>(IEnumerable<T> messages, string topicName, Func<T, string> keySelector = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task SubscribeAsync<T>(string topicName, string consumerGroup, Func<T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task SubscribePatternAsync<T>(string topicPattern, string consumerGroup, Func<string, T, Task> messageHandler, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task<StreamResult<T>> ProcessAsync<T>(string topicName, string consumerGroup, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task CreateTopicAsync(string topicName, int partitions = 1, short replicationFactor = 1, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task<List<string>> ListTopicsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task SetTopicRetentionAsync(string topicName, TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public Task SetMessageExpiryAsync(string topicName, TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Kafka provider not available. Please install OElite.Restme.Kafka package and configure Kafka connection.");
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    /// <summary>
    /// Default OpenSearch provider that throws helpful error messages
    /// </summary>
    public class DefaultSearchProvider : ISearchProvider
    {
        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "DefaultSearch";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => new (RestMode.Memory);

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.Search;

        public Task IndexAsync<T>(T document, string indexName = null, string documentId = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task BulkIndexAsync<T>(IEnumerable<T> documents, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<T> GetAsync<T>(string documentId, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<SearchResult<T>> SearchAsync<T>(SearchQuery query, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<SearchResult<T>> TextSearchAsync<T>(string searchText, string[] fields = null, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<bool> DeleteAsync(string documentId, string indexName = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<bool> DeleteByQueryAsync<T>(Expression<Func<T, bool>> query, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<AggregationResult> AggregateAsync<T>(AggregationQuery query, string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task CreateIndexAsync<T>(string indexName = null, OpenSearchMapping mapping = null, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task<List<string>> ListIndicesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task SetIndexTTLAsync(string indexName, string ttlField, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public Task SetIndexLifecyclePolicyAsync(string indexName, TimeSpan deleteAfter, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("OpenSearch provider not available. Please install OElite.Restme.OpenSearch package and configure OpenSearch connection.");
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }
}