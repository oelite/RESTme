using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using OElite.Restme.Abstractions;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// ClickHouse provider implementation with LINQ expression support
    /// </summary>
    public class ClickHouseProvider : IColumnarProvider
    {
        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "ClickHouse";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration { get; }

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.Columnar;

        private readonly ClickHouseConnectionWrapper _connection;
        private bool _disposed = false;

        public ClickHouseProvider(RestConfig config)
        {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));

            // Use RestConfig authentication fields to build connection string
            var username = config.AuthKey;
            var password = config.AuthSecret;

            string effectiveConnectionString;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                // Build connection string with authentication from config
                var baseConnection = config.ConnectionString ?? "clickhouse://localhost:8123";
                if (!baseConnection.Contains("@"))
                {
                    // Insert credentials into connection string
                    var protocolEnd = baseConnection.IndexOf("://", StringComparison.Ordinal);
                    if (protocolEnd >= 0)
                    {
                        var protocol = baseConnection.Substring(0, protocolEnd + 3);
                        var rest = baseConnection.Substring(protocolEnd + 3);
                        effectiveConnectionString = $"{protocol}{username}:{password}@{rest}";
                    }
                    else
                    {
                        effectiveConnectionString = baseConnection;
                    }
                }
                else
                {
                    effectiveConnectionString = baseConnection;
                }
            }
            else
            {
                // Use connection string as-is (may contain authentication)
                effectiveConnectionString = config.ConnectionString ?? "clickhouse://localhost:8123";
            }

            _connection = new ClickHouseConnectionWrapper(effectiveConnectionString);
        }

        public async Task InsertAsync<T>(T data, string tableName = null, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            tableName ??= GetTableName<T>();
            
            // Generate INSERT statement from object properties
            var properties = typeof(T).GetProperties();
            var columnNames = string.Join(", ", properties.Select(p => p.Name));
            var values = properties.Select(p => FormatValue(p.GetValue(data)));
            var valuesStr = string.Join(", ", values);
            
            var sql = $"INSERT INTO {tableName} ({columnNames}) VALUES ({valuesStr})";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> data, string tableName = null, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            tableName ??= GetTableName<T>();
            await _connection.BulkInsertAsync(data, tableName, cancellationToken);
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return "NULL";

            return value switch
            {
                string s => $"'{s.Replace("'", "''")}'",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                bool b => b ? "1" : "0",
                Guid g => $"'{g}'",
                decimal d => d.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture),
                float f => f.ToString("0.################", System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "NULL"
            };
        }

        public async Task<List<T>> QueryAsync<T>(string sql, object parameters = null, CancellationToken cancellationToken = default)
        {
            return await _connection.ExecuteQueryAsync<T>(sql, cancellationToken);
        }

        public async Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, string tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= GetTableName<T>();
            
            // Use ClickHouseExpressionTranslator to convert LINQ to SQL
            var translator = new ClickHouseExpressionTranslator();
            var whereClause = translator.Translate(predicate);
            
            var sql = $"SELECT * FROM {tableName} WHERE {whereClause}";
            return await QueryAsync<T>(sql, null, cancellationToken);
        }

        public async Task<long> CountAsync<T>(Expression<Func<T, bool>> predicate = null, string tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= GetTableName<T>();
            var sql = $"SELECT COUNT(*) FROM {tableName}";
            var results = await QueryAsync<long>(sql, null, cancellationToken);
            return results.FirstOrDefault();
        }

        public async Task<long> CountAsync(string tableName, string whereClause = null, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT COUNT(*) FROM {tableName}";
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += $" WHERE {whereClause}";
            }
            var results = await QueryAsync<long>(sql, null, cancellationToken);
            return results.FirstOrDefault();
        }

        public async Task<TimeSeriesResult<T>> TimeSeriesAsync<T>(string tableName, DateTime start, DateTime end, string groupBy = null, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {tableName} WHERE timestamp >= '{start:yyyy-MM-dd HH:mm:ss}' AND timestamp < '{end:yyyy-MM-dd HH:mm:ss}'";
            if (!string.IsNullOrEmpty(groupBy))
            {
                sql += $" ORDER BY {groupBy}";
            }

            var data = await QueryAsync<T>(sql, null, cancellationToken);

            return new TimeSeriesResult<T>
            {
                Data = data,
                QueryDuration = TimeSpan.Zero,
                TotalRecords = data.Count
            };
        }

        public async Task<AggregationResult> AggregateAsync(string tableName, string aggregationQuery, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT {aggregationQuery} FROM {tableName}";
            var results = await QueryAsync<Dictionary<string, object>>(sql, null, cancellationToken);

            return new AggregationResult
            {
                Aggregations = results.FirstOrDefault() ?? new Dictionary<string, object>()
            };
        }

        public async Task CreateTableAsync<T>(string tableName = null, ClickHouseEngine engine = ClickHouseEngine.MergeTree, CancellationToken cancellationToken = default)
        {
            tableName ??= GetTableName<T>();
            var engineName = engine.ToString();
            
            // Generate schema from type T
            var schema = GenerateTableSchema<T>();
            var sql = $"CREATE TABLE IF NOT EXISTS {tableName} ({schema}) ENGINE = {engineName}() ORDER BY tuple()";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }

        private static string GenerateTableSchema<T>()
        {
            var properties = typeof(T).GetProperties();
            var columns = new List<string>();
            
            foreach (var property in properties)
            {
                var columnName = property.Name;
                var clickHouseType = MapToClickHouseType(property.PropertyType);
                columns.Add($"{columnName} {clickHouseType}");
            }
            
            return string.Join(", ", columns);
        }

        private static string MapToClickHouseType(Type type)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            
            if (underlyingType == typeof(string))
                return "String";
            if (underlyingType == typeof(int))
                return "Int32";
            if (underlyingType == typeof(long))
                return "Int64";
            if (underlyingType == typeof(short))
                return "Int16";
            if (underlyingType == typeof(byte))
                return "UInt8";
            if (underlyingType == typeof(uint))
                return "UInt32";
            if (underlyingType == typeof(ulong))
                return "UInt64";
            if (underlyingType == typeof(float))
                return "Float32";
            if (underlyingType == typeof(double))
                return "Float64";
            if (underlyingType == typeof(decimal))
                return "Decimal(18, 2)";
            if (underlyingType == typeof(bool))
                return "UInt8";
            if (underlyingType == typeof(DateTime))
                return "DateTime";
            if (underlyingType == typeof(DateTimeOffset))
                return "DateTime64(3)";
            if (underlyingType == typeof(Guid))
                return "UUID";
            if (underlyingType == typeof(TimeSpan))
                return "Int64";
            
            // Default to String for unknown types
            return "String";
        }

        public async Task SetTableTTLAsync(string tableName, string ttlExpression, CancellationToken cancellationToken = default)
        {
            var sql = $"ALTER TABLE {tableName} MODIFY TTL {ttlExpression}";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }

        public async Task CreateTTLIndexAsync(string tableName, string columnName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            // ClickHouse TTL syntax: ALTER TABLE table_name MODIFY TTL column + INTERVAL ttl_value unit
            var sql = $"ALTER TABLE {tableName} MODIFY TTL {columnName} + INTERVAL {ttl.TotalSeconds} SECOND";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }



        public async Task DropTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            var sql = $"DROP TABLE IF EXISTS {tableName}";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }

        public async Task TruncateTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            var sql = $"TRUNCATE TABLE {tableName}";
            await _connection.ExecuteNonQueryAsync(sql, cancellationToken);
        }

        private static string GetTableName<T>()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name.ToLower();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}