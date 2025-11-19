using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OElite.Abstractions;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// Builds ClickHouse SQL statements
    /// </summary>
    public static class ClickHouseSqlBuilder
    {
        public static string BuildInsertSql<T>(T data, string tableName)
        {
            var properties = GetProperties<T>();
            var columns = string.Join(", ", properties.Select(p => ToSnakeCase(p.Name)));
            var values = string.Join(", ", properties.Select((p, i) => $"@p{i}"));

            return $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
        }

        public static string BuildBulkInsertSql<T>(string tableName)
        {
            var properties = GetProperties<T>();
            var columns = string.Join(", ", properties.Select(p => ToSnakeCase(p.Name)));

            return $"INSERT INTO {tableName} ({columns}) VALUES ";
        }

        private static PropertyInfo[] GetProperties<T>()
        {
            return typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();
        }

        private static string ToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            var result = new StringBuilder();
            for (int i = 0; i < pascalCase.Length; i++)
            {
                char currentChar = pascalCase[i];
                if (char.IsUpper(currentChar) && i > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(currentChar));
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// Builds parameters for ClickHouse queries
    /// </summary>
    public static class ClickHouseParameterBuilder
    {
        public static Dictionary<string, object> BuildParameters<T>(T data)
        {
            var parameters = new Dictionary<string, object>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            for (int i = 0; i < properties.Length; i++)
            {
                var value = properties[i].GetValue(data);
                parameters[$"@p{i}"] = value ?? DBNull.Value;
            }

            return parameters;
        }

        public static Dictionary<string, object> BuildBulkParameters<T>(IEnumerable<T> data)
        {
            var parameters = new Dictionary<string, object>();
            var list = data.ToList();

            if (!list.Any()) return parameters;

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            for (int rowIndex = 0; rowIndex < list.Count; rowIndex++)
            {
                for (int colIndex = 0; colIndex < properties.Length; colIndex++)
                {
                    var value = properties[colIndex].GetValue(list[rowIndex]);
                    parameters[$"@p{rowIndex}_{colIndex}"] = value ?? DBNull.Value;
                }
            }

            return parameters;
        }
    }

    /// <summary>
    /// Builds ClickHouse table creation SQL
    /// </summary>
    public static class ClickHouseTableBuilder
    {
        public static string BuildCreateTableSql<T>(string tableName, ClickHouseEngine engine)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            var columns = new List<string>();
            foreach (var property in properties)
            {
                var columnName = ToSnakeCase(property.Name);
                var columnType = MapClrTypeToClickHouseType(property.PropertyType);
                columns.Add($"{columnName} {columnType}");
            }

            var engineClause = GetEngineClause(engine);
            var columnsSql = string.Join(",\n    ", columns);

            return $@"
CREATE TABLE IF NOT EXISTS {tableName} (
    {columnsSql}
) ENGINE = {engineClause}
ORDER BY tuple()";
        }

        private static string MapClrTypeToClickHouseType(Type clrType)
        {
            if (clrType == typeof(string))
                return "String";
            if (clrType == typeof(int) || clrType == typeof(int?))
                return "Int32";
            if (clrType == typeof(long) || clrType == typeof(long?))
                return "Int64";
            if (clrType == typeof(decimal) || clrType == typeof(decimal?))
                return "Decimal(18,2)";
            if (clrType == typeof(double) || clrType == typeof(double?))
                return "Float64";
            if (clrType == typeof(float) || clrType == typeof(float?))
                return "Float32";
            if (clrType == typeof(bool) || clrType == typeof(bool?))
                return "UInt8";
            if (clrType == typeof(DateTime) || clrType == typeof(DateTime?))
                return "DateTime";
            if (clrType == typeof(Guid))
                return "String";

            // Default to String for unknown types
            return "String";
        }

        private static string GetEngineClause(ClickHouseEngine engine)
        {
            return engine switch
            {
                ClickHouseEngine.MergeTree => "MergeTree()",
                ClickHouseEngine.ReplacingMergeTree => "ReplacingMergeTree()",
                ClickHouseEngine.SummingMergeTree => "SummingMergeTree()",
                ClickHouseEngine.AggregatingMergeTree => "AggregatingMergeTree()",
                ClickHouseEngine.CollapsingMergeTree => "CollapsingMergeTree(sign)",
                ClickHouseEngine.VersionedCollapsingMergeTree => "VersionedCollapsingMergeTree(sign, version)",
                ClickHouseEngine.GraphiteMergeTree => "GraphiteMergeTree('config')",
                _ => "MergeTree()"
            };
        }

        private static string ToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            var result = new StringBuilder();
            for (int i = 0; i < pascalCase.Length; i++)
            {
                char currentChar = pascalCase[i];
                if (char.IsUpper(currentChar) && i > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(currentChar));
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// Simplified ClickHouse connection (would use actual ClickHouse client in real implementation)
    /// </summary>
    public class ClickHouseConnection : IDisposable
    {
        private readonly string _connectionString;
        private bool _disposed = false;

        public ClickHouseConnection(string connectionString)
        {
            _connectionString = connectionString;
        }

        public Task ExecuteNonQueryAsync(string sql, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "ClickHouse client implementation requires ClickHouse.Client or similar package. " +
                "Please install the appropriate ClickHouse client library and implement the connection logic.");
        }

        public async Task<List<T>> ExecuteQueryAsync<T>(string sql, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "ClickHouse client implementation requires ClickHouse.Client or similar package. " +
                "Please install the appropriate ClickHouse client library and implement the connection logic.");
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException(
                "ClickHouse client implementation requires ClickHouse.Client or similar package. " +
                "Please install the appropriate ClickHouse client library and implement the connection logic.");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose connection resources
                _disposed = true;
            }
        }
    }
}