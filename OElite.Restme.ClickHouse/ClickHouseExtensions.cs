using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite
{
    /// <summary>
    /// ClickHouse extension methods for IRestme
    /// </summary>
    public static class ClickHouseRestmeExtensions
    {
        /// <summary>
        /// Insert a single record into ClickHouse table
        /// </summary>
        public static async Task InsertAsync<T>(this IRestme rest, T data,
            string tableName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            await rest.ColumnarProvider.InsertAsync(data, tableName, cancellationToken);
        }

        /// <summary>
        /// Bulk insert multiple records into ClickHouse table
        /// </summary>
        public static async Task BulkInsertAsync<T>(this IRestme rest, IEnumerable<T> data,
            string tableName = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            await rest.ColumnarProvider.BulkInsertAsync(data, tableName, cancellationToken);
        }

        /// <summary>
        /// Execute a LINQ expression query against ClickHouse
        /// </summary>
        public static async Task<List<T>> QueryAsync<T>(this IRestme rest,
            Expression<Func<T, bool>> predicate, string tableName = null,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            return await rest.ColumnarProvider.QueryAsync(predicate, tableName, cancellationToken);
        }

        /// <summary>
        /// Execute a SQL query against ClickHouse
        /// </summary>
        public static async Task<List<T>> QueryAsync<T>(this IRestme rest,
            string sql, object parameters = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            return await rest.ColumnarProvider.QueryAsync<T>(sql, parameters, cancellationToken);
        }

        /// <summary>
        /// Count records in a ClickHouse table
        /// </summary>
        public static async Task<long> CountAsync(this IRestme rest, string tableName,
            string whereClause = null, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            return await rest.ColumnarProvider.CountAsync(tableName, whereClause, cancellationToken);
        }

        /// <summary>
        /// Execute time-series queries with automatic date filtering
        /// </summary>
        public static async Task<TimeSeriesResult<T>> TimeSeriesAsync<T>(this IRestme rest,
            string tableName, DateTime start, DateTime end, string groupBy = null,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            return await rest.ColumnarProvider.TimeSeriesAsync<T>(
                tableName, start, end, groupBy, cancellationToken);
        }

        /// <summary>
        /// Execute aggregation queries for analytics
        /// </summary>
        public static async Task<AggregationResult> AggregateAsync(this IRestme rest,
            string tableName, string aggregationQuery, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            return await rest.ColumnarProvider.AggregateAsync(tableName, aggregationQuery, cancellationToken);
        }

        /// <summary>
        /// Create a ClickHouse table with automatic schema inference
        /// </summary>
        public static async Task CreateTableAsync<T>(this IRestme rest,
            string tableName = null, ClickHouseEngine engine = ClickHouseEngine.MergeTree,
            CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            await rest.ColumnarProvider.CreateTableAsync<T>(tableName, engine, cancellationToken);
        }

        /// <summary>
        /// Set TTL (Time To Live) configuration for a ClickHouse table
        /// </summary>
        public static async Task SetTableTTLAsync(this IRestme rest, string tableName,
            string ttlExpression, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            await rest.ColumnarProvider.SetTableTTLAsync(tableName, ttlExpression, cancellationToken);
        }

        /// <summary>
        /// Create a TTL index for automatic record expiration
        /// </summary>
        public static async Task CreateTTLIndexAsync(this IRestme rest, string tableName,
            string columnName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (rest.CurrentMode != RestMode.ClickHouse)
                throw new OEliteException("ClickHouse mode required");

            if (rest.ColumnarProvider == null)
                throw new OEliteException("ClickHouse provider not initialized");

            await rest.ColumnarProvider.CreateTTLIndexAsync(tableName, columnName, ttl, cancellationToken);
        }
    }
}