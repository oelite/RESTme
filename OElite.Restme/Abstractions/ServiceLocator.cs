using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OElite.Abstractions
{
    /// <summary>
    /// Service locator for dynamic provider loading
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IServiceFactory> _factories = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// Register a service factory for a specific provider type
        /// </summary>
        public static void RegisterFactory<T>(IServiceFactory factory) where T : class
        {
            lock (_lock)
            {
                _factories[typeof(T)] = factory;
            }
        }

        /// <summary>
        /// Get a service factory for a specific provider type
        /// </summary>
        public static IServiceFactory? GetFactory<T>() where T : class
        {
            lock (_lock)
            {
                _factories.TryGetValue(typeof(T), out var factory);
                return factory;
            }
        }

        /// <summary>
        /// Clear all registered factories
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _factories.Clear();
            }
        }

        /// <summary>
        /// Get or create a service factory with fallback to default
        /// </summary>
        public static IServiceFactory GetOrCreateFactory(ILogger? logger = null)
        {
            // For now, return the default factory
            // In a real implementation, this could check for registered factories
            return new DefaultServiceFactory(logger);
        }
    }
}
