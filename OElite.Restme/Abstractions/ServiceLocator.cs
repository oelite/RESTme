using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace OElite.Restme.Abstractions
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
                
                // If not found, try to auto-discover providers
                if (factory == null)
                {
                    DiscoverAndRegisterProviders();
                    _namedFactories.TryGetValue(name.ToLowerInvariant(), out factory);
                }
                
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

        /// <summary>
        /// Automatically discover and register provider assemblies
        /// </summary>
        private static void DiscoverAndRegisterProviders()
        {
            try
            {
                // Get all loaded assemblies that might contain providers
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && a.FullName != null)
                    .Where(a => a.FullName.Contains("OElite.Restme") &&
                               !a.FullName.Contains("OElite.Restme.Utils") &&
                               !a.FullName.EndsWith("OElite.Restme"))
                    .ToList();

                // Try multiple directory paths for assembly discovery
                var searchPaths = new List<string>();

                // Current executing assembly location
                var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (currentDirectory != null)
                    searchPaths.Add(currentDirectory);

                // Entry assembly location (for when used as NuGet package)
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var entryDirectory = Path.GetDirectoryName(entryAssembly.Location);
                    if (entryDirectory != null && !searchPaths.Contains(entryDirectory))
                        searchPaths.Add(entryDirectory);
                }

                // Current domain base directory
                if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory) &&
                    !searchPaths.Contains(AppDomain.CurrentDomain.BaseDirectory))
                    searchPaths.Add(AppDomain.CurrentDomain.BaseDirectory);

                foreach (var searchPath in searchPaths)
                {
                    try
                    {
                        var providerFiles = Directory.GetFiles(searchPath, "OElite.Restme.*.dll", SearchOption.TopDirectoryOnly)
                            .Where(f => !f.EndsWith("OElite.Restme.Utils.dll") && !f.EndsWith("OElite.Restme.dll"));

                        foreach (var file in providerFiles)
                        {
                            try
                            {
                                var assembly = Assembly.LoadFrom(file);
                                if (!assemblies.Any(a => a.FullName == assembly.FullName))
                                {
                                    assemblies.Add(assembly);
                                }
                            }
                            catch
                            {
                                // Ignore failed assembly loads
                            }
                        }
                    }
                    catch
                    {
                        // Ignore directory access failures
                    }
                }

                // Scan assemblies for IServiceFactory implementations
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var factoryTypes = assembly.GetTypes()
                            .Where(t => typeof(IServiceFactory).IsAssignableFrom(t) && 
                                       !t.IsInterface && !t.IsAbstract)
                            .ToList();

                        foreach (var factoryType in factoryTypes)
                        {
                            // Try to trigger static constructor if present
                            try
                            {
                                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(factoryType.TypeHandle);
                            }
                            catch
                            {
                                // If static constructor fails, try manual registration
                                try
                                {
                                    var factory = Activator.CreateInstance(factoryType) as IServiceFactory;
                                    if (factory != null)
                                    {
                                        // Try to determine provider name from type name
                                        var providerName = ExtractProviderName(factoryType.Name);
                                        if (!string.IsNullOrEmpty(providerName))
                                        {
                                            RegisterFactory(providerName, factory);
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore registration failures
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore type scanning failures
                    }
                }
            }
            catch
            {
                // Ignore discovery failures - fallback to default behavior
            }
        }

        /// <summary>
        /// Extract provider name from factory type name
        /// </summary>
        private static string ExtractProviderName(string typeName)
        {
            // RabbitMQServiceFactory -> rabbitmq
            // AzureServiceFactory -> azure
            // S3ServiceFactory -> s3
            if (typeName.EndsWith("ServiceFactory"))
            {
                var providerName = typeName.Substring(0, typeName.Length - "ServiceFactory".Length);
                return providerName.ToLowerInvariant();
            }
            return string.Empty;
        }
    }
}
