using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// ClickHouse connection wrapper using ClickHouse.Client library
    /// </summary>
    public class ClickHouseConnectionWrapper : IDisposable
    {
        private readonly ClickHouseConnection _connection;
        private bool _disposed = false;

        public ClickHouseConnectionWrapper(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            // Parse Restme connection string format: clickhouse://host:port/database
            var parsedConnection = ParseConnectionString(connectionString);

            Console.WriteLine($"[DEBUG] Input connection string: {connectionString}");
            Console.WriteLine($"[DEBUG] Parsed connection string: {parsedConnection}");

            // ClickHouse.Client expects standard ADO.NET format
            _connection = new ClickHouseConnection(parsedConnection);
        }

        private static string ParseConnectionString(string connectionString)
        {
            // Convert from clickhouse://host:port/database to ClickHouse.Client format
            var uri = connectionString.Replace("clickhouse://", "");
            
            // Default values
            var host = "localhost";
            var port = "8123";
            var database = "default";
            var username = "";
            var password = "";

            // Parse authentication if present: username:password@host
            if (uri.Contains("@"))
            {
                var authParts = uri.Split('@');
                var credentials = authParts[0].Split(':');
                username = credentials[0];
                password = credentials.Length > 1 ? credentials[1] : "";
                uri = authParts[1];
            }

            // Parse host, port, database
            var parts = uri.Split('/');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                var hostPort = parts[0].Split(':');
                host = hostPort[0];
                port = hostPort.Length > 1 ? hostPort[1] : "8123";
            }

            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                database = parts[1].Split('?')[0];
            }

            // Build ClickHouse.Client connection string
            var builder = new System.Text.StringBuilder();
            builder.Append($"Host={host};Port={port};Database={database}");
            
            if (!string.IsNullOrEmpty(username))
                builder.Append($";Username={username}");
            
            if (!string.IsNullOrEmpty(password))
                builder.Append($";Password={password}");

            builder.Append(";Compress=false");

            return builder.ToString();
        }

        public async Task ExecuteNonQueryAsync(string query, CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);
            using var command = _connection.CreateCommand();
            command.CommandText = query;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<List<T>> ExecuteQueryAsync<T>(string query, CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);
            using var command = _connection.CreateCommand();
            command.CommandText = query;

            var results = new List<T>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                // Simple object mapping - for complex types, you'd use reflection
                if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T) == typeof(long))
                {
                    var value = reader.GetValue(0);

                    // Handle type conversions for ClickHouse-specific types
                    if (typeof(T) == typeof(long) && value is ulong ulongValue)
                    {
                        results.Add((T)(object)(long)ulongValue);
                    }
                    else if (typeof(T) == typeof(int) && value is ulong ulongValueInt)
                    {
                        results.Add((T)(object)(int)ulongValueInt);
                    }
                    else
                    {
                        results.Add((T)value);
                    }
                }
                else
                {
                    // For complex types, create instance and map properties
                    var instance = Activator.CreateInstance<T>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var property = typeof(T).GetProperty(reader.GetName(i));
                        if (property != null && !reader.IsDBNull(i))
                        {
                            property.SetValue(instance, reader.GetValue(i));
                        }
                    }
                    results.Add(instance);
                }
            }

            return results;
        }

        private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
        {
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
            }
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> data, string tableName, CancellationToken cancellationToken = default)
        {
            await EnsureConnectionOpenAsync(cancellationToken);
            
            // Use ClickHouse bulk insert with VALUES format
            var dataList = data.ToList();
            if (!dataList.Any()) return;

            // Get properties from first item
            var properties = typeof(T).GetProperties();
            var columnNames = string.Join(", ", properties.Select(p => p.Name));
            
            // Build bulk insert values
            var valuesBuilder = new System.Text.StringBuilder();
            valuesBuilder.Append($"INSERT INTO {tableName} ({columnNames}) VALUES ");
            
            var valueRows = new List<string>();
            foreach (var item in dataList)
            {
                var values = properties.Select(p => FormatValue(p.GetValue(item)));
                valueRows.Add($"({string.Join(", ", values)})");
            }
            
            valuesBuilder.Append(string.Join(", ", valueRows));
            
            using var command = _connection.CreateCommand();
            command.CommandText = valuesBuilder.ToString();
            await command.ExecuteNonQueryAsync(cancellationToken);
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