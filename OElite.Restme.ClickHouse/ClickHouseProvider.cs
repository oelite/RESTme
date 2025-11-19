using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// ClickHouse provider implementation with LINQ expression support
    /// </summary>
    public class ClickHouseProvider : IColumnarProvider
    {
        private readonly ClickHouseConnection _connection;
        private readonly RestConfig _config;
        private bool _disposed = false;

        public ClickHouseProvider(string connectionString, RestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connection = new ClickHouseConnection(connectionString);
        }

        public async Task InsertAsync<T>(T data, string tableName = null, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            tableName ??= GetTableName<T>();
            var sql = ClickHouseSqlBuilder.BuildInsertSql(data, tableName);
            var parameters = ClickHouseParameterBuilder.BuildParameters(data);

            await _connection.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> data, string tableName = null, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            tableName ??= GetTableName<T>();
            var sql = ClickHouseSqlBuilder.BuildBulkInsertSql<T>(tableName);
            var parameters = ClickHouseParameterBuilder.BuildBulkParameters(data);

            await _connection.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task<List<T>> QueryAsync<T>(string sql, object parameters = null, CancellationToken cancellationToken = default)
        {
            var paramDict = ConvertToDictionary(parameters);
            return await _connection.ExecuteQueryAsync<T>(sql, paramDict, cancellationToken);
        }

        public async Task<List<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, string tableName = null, CancellationToken cancellationToken = default)
        {
            tableName ??= GetTableName<T>();

            // Translate LINQ expression to ClickHouse SQL
            var expressionTranslator = new ClickHouseExpressionTranslator();
            var whereClause = expressionTranslator.Translate(predicate);

            var sql = $"SELECT * FROM {tableName} WHERE {whereClause}";
            var parameters = expressionTranslator.GetParameters();

            return await _connection.ExecuteQueryAsync<T>(sql, parameters, cancellationToken);
        }

        public async Task<long> CountAsync(string tableName, string whereClause = null, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT COUNT(*) FROM {tableName}";
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += $" WHERE {whereClause}";
            }

            var result = await _connection.ExecuteScalarAsync<long>(sql, null, cancellationToken);
            return result;
        }

        public async Task<TimeSeriesResult<T>> TimeSeriesAsync<T>(string tableName, DateTime start, DateTime end, string groupBy = null, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object>
            {
                ["start"] = start,
                ["end"] = end
            };

            var groupByClause = string.IsNullOrEmpty(groupBy) ? "" : $"GROUP BY {groupBy}";
            var sql = $@"
                SELECT 
                    toStartOfHour(timestamp) as hour,
                    count() as count,
                    {groupBy ?? "1"} as group_key
                FROM {tableName} 
                WHERE timestamp >= @start AND timestamp < @end 
                {groupByClause}
                ORDER BY hour";

            var data = await _connection.ExecuteQueryAsync<T>(sql, parameters, cancellationToken);

            return new TimeSeriesResult<T>
            {
                Data = data,
                QueryDuration = TimeSpan.Zero, // Would be measured in real implementation
                TotalRecords = data.Count
            };
        }

        public async Task<AggregationResult> AggregateAsync(string tableName, string aggregationQuery, CancellationToken cancellationToken = default)
        {
            var result = await _connection.ExecuteQueryAsync<Dictionary<string, object>>(
                $"SELECT {aggregationQuery} FROM {tableName}", null, cancellationToken);

            return new AggregationResult
            {
                Aggregations = result.FirstOrDefault() ?? new Dictionary<string, object>(),
                QueryDuration = TimeSpan.Zero, // Would be measured in real implementation
                ProcessedRecords = 0 // Would be measured in real implementation
            };
        }

        public async Task CreateTableAsync<T>(string tableName = null, ClickHouseEngine engine = ClickHouseEngine.MergeTree, CancellationToken cancellationToken = default)
        {
            tableName ??= GetTableName<T>();
            var sql = ClickHouseTableBuilder.BuildCreateTableSql<T>(tableName, engine);
            await _connection.ExecuteNonQueryAsync(sql, null, cancellationToken);
        }

        public async Task SetTableTTLAsync(string tableName, string ttlExpression, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(ttlExpression)) throw new ArgumentNullException(nameof(ttlExpression));

            var sql = $"ALTER TABLE {tableName} MODIFY TTL {ttlExpression}";
            await _connection.ExecuteNonQueryAsync(sql, null, cancellationToken);
        }

        public async Task CreateTTLIndexAsync(string tableName, string columnName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException(nameof(columnName));

            var indexName = $"ttl_idx_{tableName}_{columnName}";
            var ttlSeconds = (long)ttl.TotalSeconds;
            var sql = $"ALTER TABLE {tableName} ADD INDEX {indexName} ({columnName}) TYPE minmax GRANULARITY 1";

            await _connection.ExecuteNonQueryAsync(sql, null, cancellationToken);

            // Set TTL on the table
            var ttlSql = $"ALTER TABLE {tableName} MODIFY TTL {columnName} + INTERVAL {ttlSeconds} SECOND";
            await _connection.ExecuteNonQueryAsync(ttlSql, null, cancellationToken);
        }

        private string GetTableName<T>()
        {
            // Use attribute-based table naming or default to type name
            var type = typeof(T);
            var attribute = type.GetCustomAttributes(typeof(ClickHouseTableAttribute), false)
                .FirstOrDefault() as ClickHouseTableAttribute;
            return attribute?.TableName ?? type.Name.ToLowerInvariant();
        }

        private Dictionary<string, object> ConvertToDictionary(object parameters)
        {
            if (parameters == null)
                return new Dictionary<string, object>();

            if (parameters is Dictionary<string, object> dict)
                return dict;

            // Handle anonymous objects or POCOs
            var result = new Dictionary<string, object>();
            var properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var value = prop.GetValue(parameters);
                result[prop.Name] = value ?? DBNull.Value;
            }

            return result;
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

    /// <summary>
    /// Attribute to specify ClickHouse table name
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ClickHouseTableAttribute : Attribute
    {
        public string TableName { get; }

        public ClickHouseTableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}