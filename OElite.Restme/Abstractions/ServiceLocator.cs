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
        private static readonly Dictionary<string, IServiceFactory> _namedFactories = new();
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
        /// Register a service factory by name
        /// </summary>
        public static void RegisterFactory(string name, IServiceFactory factory)
        {
            lock (_lock)
            {
                _namedFactories[name.ToLowerInvariant()] = factory;
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
        /// Get a service factory by name
        /// </summary>
        public static IServiceFactory? GetFactory(string name)
        {
            lock (_lock)
            {
                _namedFactories.TryGetValue(name.ToLowerInvariant(), out var factory);
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
                _namedFactories.Clear();
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
