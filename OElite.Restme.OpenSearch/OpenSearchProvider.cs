using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using OElite.Restme.Abstractions;
using OpenSearch.Net;
using OpenSearch.Client;

namespace OElite.Restme.OpenSearch
{
    /// <summary>
    /// OpenSearch provider implementation
    /// Provides simplified access to search, indexing, and analytics capabilities
    /// </summary>
    public class OpenSearchProvider : ISearchProvider
    {
        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "OpenSearch";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration { get; }

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.Search;

        private readonly OpenSearchClient _client;
        private readonly string _defaultIndex;
        private bool _disposed = false;

        public OpenSearchProvider(RestConfig config)
        {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));

            Console.WriteLine($"OpenSearchProvider constructor called with connectionString: '{config.ConnectionString}'");

            // Parse connection string and create client
            var parsedUri = ParseConnectionString(config.ConnectionString ?? "opensearch://localhost:9200");
            Console.WriteLine($"Parsed URI: {parsedUri}");

            var settings = new ConnectionSettings(parsedUri)
                .ServerCertificateValidationCallback((o, cert, chain, errors) => true) // accept self-signed
                .DefaultMappingFor(typeof(object), i => i.IndexName("default"));

            var username = config.AuthKey;
            var password = config.AuthSecret;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                settings = settings.BasicAuthentication(username, password);
            }

            _client = new OpenSearchClient(settings);
            _defaultIndex = "default";
            Console.WriteLine("OpenSearchProvider initialized successfully");
        }

        private static Uri ParseConnectionString(string connectionString)
        {
            try
            {
                // Debug: Log the incoming connection string
                Console.WriteLine($"OpenSearch ParseConnectionString received: '{connectionString}'");

                // Handle different connection string formats
                string uriString;

                if (connectionString.StartsWith("http://") || connectionString.StartsWith("https://"))
                {
                    // Already a valid HTTP URI
                    uriString = connectionString;
                }
                else if (connectionString.Contains("://"))
                {
                    // Replace scheme with http
                    var parts = connectionString.Split("://", 2);
                    uriString = "http://" + parts[1];
                }
                else
                {
                    // Assume it's just host:port
                    uriString = "http://" + connectionString;
                }

                Console.WriteLine($"OpenSearch ParseConnectionString processed: '{uriString}'");

                return new Uri(uriString);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"OpenSearch ParseConnectionString failed for: '{connectionString}'. Error: {ex.Message}");
                throw;
            }
        }

        public async Task IndexAsync<T>(T document, string indexName = null, string documentId = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            indexName ??= GetIndexName<T>();

            var response = await _client.IndexAsync(document, idx => idx
                .Index(indexName)
                .Id(documentId)
                .Refresh(Refresh.True), cancellationToken);

            if (!response.IsValid)
                throw new InvalidOperationException($"Failed to index document: {response.DebugInformation}");
        }

        public async Task BulkIndexAsync<T>(IEnumerable<T> documents, string indexName = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            indexName ??= GetIndexName<T>();

            var bulkDescriptor = new BulkDescriptor();
            foreach (var doc in documents)
            {
                bulkDescriptor.Index<object>(i => i
                    .Index(indexName)
                    .Document(doc));
            }

            var response = await _client.BulkAsync(bulkDescriptor, cancellationToken);
            if (!response.IsValid)
                throw new InvalidOperationException($"Failed to bulk index documents: {response.DebugInformation}");
        }

        public async Task<T> GetAsync<T>(string documentId, string indexName = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));
            indexName ??= GetIndexName<T>();

            var response = await _client.GetAsync<T>(documentId, g => g.Index(indexName), cancellationToken);

            if (!response.IsValid || !response.Found)
                return default!;

            return response.Source;
        }

        public async Task<SearchResult<T>> SearchAsync<T>(SearchQuery query, string indexName = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            indexName ??= GetIndexName<T>();

            // Build OpenSearch query from SearchQuery
            var searchDescriptor = new SearchDescriptor<T>()
                .Index(indexName)
                .From(query.PageIndex * query.PageSize)
                .Size(query.PageSize);

            // Apply query if provided
            if (!string.IsNullOrEmpty(query.Query))
            {
                searchDescriptor = searchDescriptor.Query(q => q
                    .QueryString(qs => qs.Query(query.Query)));
            }
            else
            {
                searchDescriptor = searchDescriptor.Query(q => q.MatchAll());
            }

            var response = await _client.SearchAsync<T>(searchDescriptor, cancellationToken);

            if (!response.IsValid)
                throw new InvalidOperationException($"Search failed: {response.DebugInformation}");

            return new SearchResult<T>
            {
                Documents = response.Documents.ToList(),
                TotalCount = (long)response.Total,
                PageSize = query.PageSize,
                PageIndex = query.PageIndex
            };
        }

        public async Task<SearchResult<T>> TextSearchAsync<T>(string searchText, string[] fields = null,
            string indexName = null, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrEmpty(searchText)) throw new ArgumentNullException(nameof(searchText));
            indexName ??= GetIndexName<T>();

            var searchDescriptor = new SearchDescriptor<T>()
                .Index(indexName)
                .Query(q => q.MultiMatch(m => m
                    .Query(searchText)
                    .Fields(fields ?? new[] { "*" })));

            var response = await _client.SearchAsync<T>(searchDescriptor, cancellationToken);

            if (!response.IsValid)
                throw new InvalidOperationException($"Text search failed: {response.DebugInformation}");

            return new SearchResult<T>
            {
                Documents = response.Documents.ToList(),
                TotalCount = (long)response.Total
            };
        }

        public async Task<bool> DeleteAsync(string documentId, string indexName = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(documentId)) throw new ArgumentNullException(nameof(documentId));
            indexName ??= _defaultIndex;

            var response = await _client.DeleteAsync(new DeleteRequest(indexName, documentId), cancellationToken);
            return response.IsValid && response.Result == Result.Deleted;
        }

        public async Task<bool> DeleteByQueryAsync<T>(Expression<Func<T, bool>> query, string indexName = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            indexName ??= GetIndexName<T>();

            // Simplified implementation - in production, translate LINQ expression to OpenSearch query
            var deleteDescriptor = new DeleteByQueryDescriptor<T>()
                .Index(indexName)
                .Query(q => q.MatchAll());

            var response = await _client.DeleteByQueryAsync(deleteDescriptor, cancellationToken);
            return response.IsValid;
        }

        public async Task<AggregationResult> AggregateAsync<T>(AggregationQuery query, string indexName = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            indexName ??= GetIndexName<T>();

            var searchDescriptor = new SearchDescriptor<T>()
                .Index(indexName)
                .Size(0)
                .Query(q => q.MatchAll());

            // Add aggregations based on query
            if (!string.IsNullOrEmpty(query.GroupBy))
            {
                searchDescriptor = searchDescriptor.Aggregations(a => a
                    .Terms(query.GroupBy, t => t.Field(query.GroupBy)));
            }

            var response = await _client.SearchAsync<T>(searchDescriptor, cancellationToken);

            if (!response.IsValid)
                throw new InvalidOperationException($"Aggregation failed: {response.DebugInformation}");

            return new AggregationResult
            {
                Aggregations = response.Aggregations?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as object) ??
                               new Dictionary<string, object>()
            };
        }

        public async Task CreateIndexAsync<T>(string indexName = null, OpenSearchMapping mapping = null,
            CancellationToken cancellationToken = default) where T : class
        {
            indexName ??= GetIndexName<T>();

            var createDescriptor = new CreateIndexDescriptor(indexName);

            if (mapping?.Settings != null && mapping.Settings.Any())
            {
                createDescriptor = createDescriptor.Settings(s =>
                {
                    foreach (var setting in mapping.Settings)
                    {
                        s.Setting(setting.Key, setting.Value);
                    }

                    return s;
                });
            }

            var response = await _client.Indices.CreateAsync(createDescriptor, cancellationToken);

            if (!response.IsValid && !response.ServerError?.Error?.Type?.Contains("resource_already_exists") == true)
                throw new InvalidOperationException($"Failed to create index: {response.DebugInformation}");
        }

        public async Task<List<string>> ListIndicesAsync(CancellationToken cancellationToken = default)
        {
            var response = await _client.Cat.IndicesAsync(new CatIndicesRequest(), cancellationToken);
            return response.Records.Select(r => r.Index).ToList();
        }

        public async Task SetIndexTTLAsync(string indexName, string ttlField, TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(indexName))
                throw new ArgumentNullException(nameof(indexName));

            var updateDescriptor = new UpdateIndexSettingsDescriptor(indexName)
                .IndexSettings(s => s.Setting("index.refresh_interval", ttl.TotalMilliseconds + "ms"));

            var response = await _client.Indices.UpdateSettingsAsync(updateDescriptor, cancellationToken);
            if (!response.IsValid)
                throw new OEliteException($"Failed to set TTL for index {indexName}: {response.DebugInformation}");
        }

        public async Task SetIndexLifecyclePolicyAsync(string indexName, TimeSpan deleteAfter,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(indexName))
                throw new ArgumentNullException(nameof(indexName));

            var updateDescriptor = new UpdateIndexSettingsDescriptor(indexName)
                .IndexSettings(s => s.Setting("index.lifecycle.rollover_alias", indexName));

            var response = await _client.Indices.UpdateSettingsAsync(updateDescriptor, cancellationToken);

            if (!response.IsValid)
                throw new InvalidOperationException($"Failed to set lifecycle policy: {response.DebugInformation}");
        }

        private string GetIndexName<T>()
        {
            return typeof(T).Name.ToLowerInvariant();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // OpenSearchClient doesn't need explicit disposal
                _disposed = true;
            }
        }
    }
}