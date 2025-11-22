using System;

namespace OElite.Restme
{
    /// <summary>
    /// Flags enum defining capabilities supported by providers.
    /// Used to determine which providers can fulfill specific requirements.
    /// </summary>
    [Flags]
    public enum ProviderCapabilities
    {
        /// <summary>
        /// No capabilities supported.
        /// </summary>
        None = 0,

        /// <summary>
        /// Supports caching operations (get, set, remove, exists).
        /// </summary>
        Cache = 1 << 0,

        /// <summary>
        /// Supports storage operations (upload, download, delete).
        /// </summary>
        Storage = 1 << 1,

        /// <summary>
        /// Supports queue operations (enqueue, dequeue, peek).
        /// </summary>
        Queue = 1 << 2,

        /// <summary>
        /// Supports columnar/analytical operations (queries, aggregations).
        /// </summary>
        Columnar = 1 << 3,

        /// <summary>
        /// Supports streaming operations (publish, subscribe, process).
        /// </summary>
        Streaming = 1 << 4,

        /// <summary>
        /// Supports search operations (index, search, delete).
        /// </summary>
        Search = 1 << 5,

        // Combined capabilities for convenience

        /// <summary>
        /// Supports both cache and storage operations.
        /// </summary>
        CacheAndStorage = Cache | Storage,

        /// <summary>
        /// Supports all capabilities.
        /// </summary>
        All = Cache | Storage | Queue | Columnar | Streaming | Search
    }
}