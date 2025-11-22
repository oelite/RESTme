using System;

namespace OElite.Restme.Abstractions
{
    /// <summary>
    /// Minimal marker interface for all Restme providers.
    /// Serves as a common base that can accumulate shared methods over time.
    /// </summary>
    public interface IRestmeProvider : IDisposable
    {
        /// <summary>
        /// Provider name/identifier for debugging and logging.
        /// Should be descriptive, e.g., "AzureCache", "S3Storage", "RedisCache".
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Configuration used to create this provider.
        /// Provides access to parsed connection details and settings.
        /// </summary>
        RestConfig Configuration { get; }

        /// <summary>
        /// Capabilities supported by this provider.
        /// Used by factories to determine if they can create this provider type.
        /// </summary>
        ProviderCapabilities Capabilities { get; }
    }
}