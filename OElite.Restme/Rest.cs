using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using OElite.Restme.Abstractions;
using OElite.Restme.Base;

namespace OElite.Restme
{
    public partial class Rest : IRestme
    {
        internal Dictionary<string, string> Params;
        internal Dictionary<string, List<string>> Headers;
        internal object? ObjAsParam;
        internal Dictionary<string, IRestmeProvider> InstantiatedProviders = new();
        public RestConfig Configuration { get; private set; }

        public string? RequestUrlPath { get; set; }
        public bool Initialized { get; private set; }
        public Uri? BaseUri { get; set; }


        /// <summary>
        /// Get a provider instance based on the current RestMode configuration using factory pattern.
        /// </summary>
        /// <typeparam name="T">The provider interface type to retrieve.</typeparam>
        /// <param name="name">Optional provider name for managing multiple instances of the same type. Defaults to "default".</param>
        /// <returns>A provider instance if available for the current mode, null otherwise.</returns>
        public T? GetProvider<T>(string name = "default") where T : class, IRestmeProvider
        {
            try
            {
                // Generate provider key using type name and custom name
                string providerKey = string.IsNullOrEmpty(name)
                    ? $"{typeof(T).Name}:default"
                    : $"{typeof(T).Name}:{name}";

                // Check if provider is already cached and still usable
                if (InstantiatedProviders.TryGetValue(providerKey, out var cachedProvider))
                {
                    // Check if the cached provider is disposed or unusable
                    if (IsProviderDisposedOrUnusable(cachedProvider))
                    {
                        Logger?.LogDebug(
                            "Cached provider {ProviderKey} is disposed, removing and creating new instance",
                            providerKey);
                        InstantiatedProviders.Remove(providerKey);
                        // Continue to create a new instance below
                    }
                    else
                    {
                        return cachedProvider as T;
                    }
                }

                // Special handling for LogProvider - always available
                if (typeof(T) == typeof(ILogProvider))
                {
                    var logProvider = new ConsoleLogProvider();
                    InstantiatedProviders[providerKey] = logProvider;
                    return logProvider as T;
                }

                // Special handling for HttpProvider - fallback implementation always available
                if (typeof(T) == typeof(IHttpProvider))
                {
                    var httpProvider = new HttpClientProvider(Configuration, Logger);
                    InstantiatedProviders[providerKey] = httpProvider;
                    return httpProvider as T;
                }

                // Special handling for Memory cache provider - built-in implementation always available
                if (typeof(T) == typeof(ICacheProvider) && Configuration.OperationMode == RestMode.Memory)
                {
                    var memoryCacheProvider = new MemoryCacheProvider();
                    InstantiatedProviders[providerKey] = memoryCacheProvider;
                    return memoryCacheProvider as T;
                }

                // Get factory based on current mode
                string factoryName = Configuration.OperationMode switch
                {
                    RestMode.Redis => "redis",
                    RestMode.RabbitMq => "rabbitmq",
                    RestMode.Azure => "azure",
                    RestMode.S3 => "s3",
                    RestMode.ClickHouse => "clickhouse",
                    RestMode.Kafka => "kafka",
                    RestMode.OpenSearch => "opensearch",
                    _ => ""
                };

                if (string.IsNullOrEmpty(factoryName))
                {
                    Logger?.LogWarning("No factory name determined for mode {Mode}", Configuration.OperationMode);
                    return null;
                }

                var factory = ServiceLocator.GetFactory(factoryName);
                if (factory == null)
                {
                    Logger?.LogWarning("Factory not found for {FactoryName}", factoryName);
                    return null;
                }

                // Check if factory can create the requested provider type
                if (!factory.CanCreateProvider<T>())
                {
                    Logger?.LogDebug("Factory {FactoryName} cannot create provider {ProviderType}", factoryName,
                        typeof(T).Name);
                    return null;
                }

                // Create provider using factory
                var provider = factory.CreateProvider<T>(Configuration);
                if (provider != null)
                {
                    Logger?.LogDebug("Successfully created {ProviderType} from {FactoryName}", typeof(T).Name,
                        factoryName);
                    // Cache the provider for future use
                    InstantiatedProviders[providerKey] = provider;
                }

                return provider;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to get provider {ProviderType}", typeof(T).Name);
                return null;
            }
        }


        public Rest(RestConfig? config = null, ILogger? logger = null)
        {
            Configuration = config ?? new RestConfig(RestMode.Memory);
            Logger = logger;

            this.PrepareRestMode();
        }

        public Rest(Uri? baseUri = null,
            string? urlPath = null, RestConfig? config = null, ILogger? logger = null,
            Dictionary<string, string>? @params = null, Dictionary<string, List<string>>? headers = null)
        {
            Configuration = config ?? new RestConfig(RestMode.Memory);
            Params = @params ?? new Dictionary<string, string>();
            Headers = headers ?? new Dictionary<string, List<string>>();
            BaseUri = baseUri!;
            RequestUrlPath = urlPath;
            Logger = logger;

            this.PrepareRestMode();
            Configuration.ConnectionString = baseUri?.Host?.IsNotNullOrEmpty() == true
                ? $"{baseUri.Scheme}://{baseUri.Host}{(baseUri.Port > 0 ? ":" + baseUri : null)}"
                : null;
        }

        public Rest(string? endPointOrConnectionString, RestConfig? configuration = null, ILogger? logger = null,
            Dictionary<string, string>? @params = null, Dictionary<string, List<string>>? headers = null)
        {
            Configuration = configuration ?? new RestConfig(RestMode.Memory);
            Params = @params ?? new Dictionary<string, string>();
            Headers = headers ?? new Dictionary<string, List<string>>();

            // Set connection string for all provider types, not just HTTP
            Configuration.ConnectionString = endPointOrConnectionString;

            if (endPointOrConnectionString != null && endPointOrConnectionString.StartsWith("http"))
                BaseUri = new Uri(endPointOrConnectionString);

            Logger = logger;
            this.PrepareRestMode();
        }


        public RestMode CurrentMode
        {
            get => Configuration.OperationMode;
            set
            {
                Configuration.OperationMode = value;
                InitializeProviders();
            }
        }

        /// <summary>
        /// Initialize provider assemblies for the current mode
        /// </summary>
        public void InitializeProviders()
        {
            try
            {
                // Load appropriate assemblies to trigger static constructors and register factories
                LoadProviderAssemblies();

                Initialized = true;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize providers for mode {Mode}", Configuration.OperationMode);
                throw new OEliteException(
                    $"Failed to initialize providers for mode {Configuration.OperationMode}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load provider assemblies to trigger static constructors for auto-registration
        /// </summary>
        private void LoadProviderAssemblies()
        {
            try
            {
                string? assemblyName = Configuration.OperationMode switch
                {
                    RestMode.Redis => "OElite.Restme.Redis",
                    RestMode.RabbitMq => "OElite.Restme.RabbitMQ",
                    RestMode.Azure => "OElite.Restme.Azure",
                    RestMode.S3 => "OElite.Restme.S3",
                    RestMode.ClickHouse => "OElite.Restme.ClickHouse",
                    RestMode.Kafka => "OElite.Restme.Kafka",
                    RestMode.OpenSearch => "OElite.Restme.OpenSearch",
                    _ => null
                };

                if (!string.IsNullOrEmpty(assemblyName))
                {
                    Logger?.LogDebug("Attempting to load provider assembly: {AssemblyName}", assemblyName);
                    var loaded = LoadAssembly(assemblyName);
                    if (loaded)
                    {
                        Logger?.LogDebug("Successfully loaded provider assembly: {AssemblyName}", assemblyName);
                    }
                    else
                    {
                        Logger?.LogWarning("Failed to load provider assembly: {AssemblyName}", assemblyName);

                        // Try discovery as a fallback
                        Logger?.LogDebug("Attempting provider auto-discovery as fallback");
                        ServiceLocator.Clear(); // Clear cache to force discovery
                        var factory = ServiceLocator.GetFactory(Configuration.OperationMode switch
                        {
                            RestMode.Redis => "redis",
                            RestMode.RabbitMq => "rabbitmq",
                            RestMode.Azure => "azure",
                            RestMode.S3 => "s3",
                            _ => ""
                        });

                        if (factory != null)
                        {
                            Logger?.LogDebug("Provider discovered through auto-discovery");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - the provider initialization will handle missing assemblies
                Logger?.LogWarning(ex, "Failed to load provider assembly for mode {Mode}", Configuration.OperationMode);
            }
        }

        /// <summary>
        /// Load an assembly by name to trigger static constructors
        /// </summary>
        private bool LoadAssembly(string assemblyName)
        {
            try
            {
                Assembly? assembly = null;

                // Check if assembly is already loaded first
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);

                if (assembly == null)
                {
                    // Try different assembly loading strategies
                    try
                    {
                        // Strategy 1: Load by name (works for GAC and referenced assemblies)
                        assembly = Assembly.Load(assemblyName);
                        Logger?.LogDebug("Loaded assembly {AssemblyName} using Assembly.Load", assemblyName);
                    }
                    catch (FileNotFoundException)
                    {
                        // Strategy 2: Try to find and load from file path
                        assembly = TryLoadAssemblyFromFile(assemblyName);
                        if (assembly != null)
                        {
                            Logger?.LogDebug("Loaded assembly {AssemblyName} from file path", assemblyName);
                        }
                    }
                }
                else
                {
                    Logger?.LogDebug("Assembly {AssemblyName} already loaded", assemblyName);
                }

                if (assembly != null)
                {
                    // Force static constructors to run by getting and accessing types
                    var types = assembly.GetTypes();

                    // Specifically look for service factory types and trigger their static constructors
                    var factoryTypes = types.Where(t =>
                        typeof(IServiceFactory).IsAssignableFrom(t) &&
                        !t.IsInterface && !t.IsAbstract).ToList();

                    foreach (var factoryType in factoryTypes)
                    {
                        try
                        {
                            // Trigger static constructor
                            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(factoryType.TypeHandle);
                            Logger?.LogDebug("Triggered static constructor for {FactoryType}", factoryType.Name);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning(ex, "Failed to trigger static constructor for {FactoryType}",
                                factoryType.Name);
                        }
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Assembly not found - this is expected if the backend package isn't referenced
                Logger?.LogDebug(ex,
                    "Provider assembly {AssemblyName} not found - backend package may not be referenced", assemblyName);
                return false;
            }
        }

        /// <summary>
        /// Try to load assembly from file in various search paths
        /// </summary>
        private Assembly? TryLoadAssemblyFromFile(string assemblyName)
        {
            var searchPaths = new List<string>();

            // Add current directory
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (currentDirectory != null)
                searchPaths.Add(currentDirectory);

            // Add entry assembly directory (for when used as NuGet package)
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                var entryDirectory = Path.GetDirectoryName(entryAssembly.Location);
                if (entryDirectory != null && !searchPaths.Contains(entryDirectory))
                    searchPaths.Add(entryDirectory);
            }

            // Add base directory
            if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory) &&
                !searchPaths.Contains(AppDomain.CurrentDomain.BaseDirectory))
                searchPaths.Add(AppDomain.CurrentDomain.BaseDirectory);

            // Add additional common deployment paths
            var additionalPaths = new List<string>
            {
                // Current working directory
                Environment.CurrentDirectory,

                // Bin directory (common in ASP.NET deployments)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin"),

                // Refs directory (for .NET Core self-contained deployments)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "refs"),

                // Runtime directory
                Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "",

                // NuGet package directories in user profile
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),

                // Global NuGet packages (Windows)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared")
            };

            foreach (var path in additionalPaths)
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !searchPaths.Contains(path))
                    searchPaths.Add(path);
            }

            Logger?.LogDebug("Searching for assembly {AssemblyName} in {PathCount} paths", assemblyName,
                searchPaths.Count);

            foreach (var searchPath in searchPaths)
            {
                try
                {
                    var assemblyPath = Path.Combine(searchPath, $"{assemblyName}.dll");
                    Logger?.LogDebug("Checking path: {AssemblyPath}", assemblyPath);

                    if (File.Exists(assemblyPath))
                    {
                        Logger?.LogDebug("Found assembly at: {AssemblyPath}", assemblyPath);
                        return Assembly.LoadFrom(assemblyPath);
                    }

                    // Also try recursive search in subdirectories for NuGet packages
                    if (searchPath.Contains(".nuget") || searchPath.Contains("packages"))
                    {
                        var foundFiles = Directory.GetFiles(searchPath, $"{assemblyName}.dll",
                            SearchOption.AllDirectories);

                        if (foundFiles.Length > 0)
                        {
                            var assemblyFile = foundFiles.First();
                            Logger?.LogDebug("Found assembly in NuGet location: {AssemblyPath}", assemblyFile);
                            return Assembly.LoadFrom(assemblyFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to load assembly from {SearchPath}", searchPath);
                    // Continue to next path
                }
            }

            Logger?.LogDebug("Assembly {AssemblyName} not found in any search paths", assemblyName);
            return null;
        }


        public void Add(object? value)
        {
            if (Params.Count > 0)
                throw new InvalidOperationException(
                    "Additional parameters have been added, try use Add(string key, object value) instead of Add(object value).");
            ObjAsParam = value;
        }

        public void Add(string key, string value)
        {
            Params[key] = value;
        }

        public void Add(string key, object value)
        {
            if (Params.ContainsKey(key))
                Params[key] = value.JsonSerialize(Configuration.UseRestConvertForCollectionSerialization,
                    Configuration.SerializerSettings);
            else
                Params.Add(key,
                    value.JsonSerialize(Configuration.UseRestConvertForCollectionSerialization,
                        Configuration.SerializerSettings));
        }

        public void AddHeader(string header, string value, bool allowMultipleValues = false)
        {
            if (Headers.ContainsKey(header))
            {
                Headers[header] = Headers[header];
                if (allowMultipleValues)
                    Headers[header].Add(value);
                else
                    Headers[header] = new List<string> { value };
            }
            else
                Headers.Add(header, new List<string> { value });
        }

        public void AddAuthorizationHeader(string token, string authTypePrefix = "Bearer ")
        {
            AddHeader("Authorization", $"{authTypePrefix}{token}");
        }


        #region GET

        public T? Get<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null) where T : class
        {
            return GetAsync<T>(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> GetAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null) where T : class
        {
            if (dataObject != null)
            {
                ObjAsParam = dataObject;
            }

            var task = Task.Run(() =>
            {
                if (!keyOrRelativeUrlPath.IsNotNullOrEmpty())
                    throw new SyntaxErrorException("No key or relative url path provided.");

                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        var httpProvider = GetProvider<IHttpProvider>();
                        if (httpProvider == null)
                            throw new InvalidOperationException("HTTP provider not available for HTTP operations.");

                        var context = CreateHttpRequestContext();
                        return httpProvider.GetAsync<T>(keyOrRelativeUrlPath, ObjAsParam)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Azure:
                    case RestMode.S3:
                    case RestMode.LocalFileSystem:
                        var storageProvider = GetProvider<IStorageProvider>();
                        if (storageProvider != null)
                        {
                            return storageProvider.GetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        throw new InvalidOperationException(
                            $"Storage provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                    case RestMode.Redis:
                    case RestMode.Memory:
                        var cacheProvider = GetProvider<ICacheProvider>();
                        if (cacheProvider != null)
                        {
                            return cacheProvider.GetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        throw new InvalidOperationException(
                            $"Cache provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                    case RestMode.RabbitMq:
                    case RestMode.Kafka:
                    default:
                        throw new NotSupportedException(
                            $"Get operation not supported for mode {CurrentMode}. Use appropriate extension methods for this provider type.");
                }
            });
            return task;
        }

        public string? Get(string? keyOrRelativePath = null, object? dataObject = null)
        {
            return GetAsync(keyOrRelativePath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<string?> GetAsync(string? keyOrRelativePath = null, object? dataObject = null)
        {
            return GetAsync<string>(keyOrRelativePath, dataObject);
        }

        #endregion

        #region PUT

        public T? Put<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class
        {
            return PostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                .WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> PutAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class
        {
            if (dataObject != null)
                ObjAsParam = dataObject;

            var task = Task.Run(() =>
            {
                // Resolve data object from parameters
                var resolvedData = ResolveDataObject<T>(dataObject);

                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        var httpProvider = GetProvider<IHttpProvider>();
                        if (httpProvider == null)
                            throw new InvalidOperationException("HTTP provider not available for HTTP operations.");

                        return httpProvider.PutAsync<T>(keyOrRelativeUrlPath, ObjAsParam, expiryInMinutes)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Azure:
                    case RestMode.S3:
                    case RestMode.LocalFileSystem:
                        var storageProvider = GetProvider<IStorageProvider>();
                        if (storageProvider == null)
                            throw new InvalidOperationException(
                                $"Storage provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        if (resolvedData == null)
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);

                        return storageProvider.PutAsync<T>(keyOrRelativeUrlPath, resolvedData)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Redis:
                    case RestMode.Memory:
                        var cacheProvider = GetProvider<ICacheProvider>();
                        if (cacheProvider == null)
                            throw new InvalidOperationException(
                                $"Cache provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        if (resolvedData == null)
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);

                        var expiry = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                        var success = cacheProvider.SetAsync(keyOrRelativeUrlPath, resolvedData, expiry)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return success ? resolvedData : null;

                    case RestMode.RabbitMq:
                    case RestMode.Kafka:
                    default:
                        throw new NotSupportedException(
                            $"Put operation not supported for mode {CurrentMode}. Use appropriate extension methods for this provider type.");
                }
            });

            return task;
        }

        public Task<string?> PutAsync(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null)
        {
            return PutAsync<string>(keyOrRelativeUrlPath, dataObject, expiryInMinutes);
        }

        public string? Put(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null)
        {
            return PutAsync(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                .WaitAndGetResult(Configuration.DefaultTimeout);
        }

        #endregion

        #region DELETE

        public T? Delete<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null) where T : class
        {
            return DeleteAsync<T>(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> DeleteAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null) where T : class
        {
            if (dataObject != null)
                ObjAsParam = dataObject;

            var task = Task.Run(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        var httpProvider = GetProvider<IHttpProvider>();
                        if (httpProvider == null)
                            throw new InvalidOperationException("HTTP provider not available for HTTP operations.");

                        return httpProvider.DeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Azure:
                    case RestMode.S3:
                    case RestMode.LocalFileSystem:
                        var storageProvider = GetProvider<IStorageProvider>();
                        if (storageProvider == null)
                            throw new InvalidOperationException(
                                $"Storage provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        var deleteSuccess = storageProvider.DeleteAsync(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return deleteSuccess ? null : null;

                    case RestMode.Redis:
                    case RestMode.Memory:
                        var cacheProvider = GetProvider<ICacheProvider>();
                        if (cacheProvider == null)
                            throw new InvalidOperationException(
                                $"Cache provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        var cacheDeleteSuccess = cacheProvider.RemoveAsync(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return cacheDeleteSuccess ? null : null;

                    case RestMode.RabbitMq:
                    case RestMode.Kafka:
                    default:
                        throw new NotSupportedException(
                            $"Delete operation not supported for mode {CurrentMode}. Use appropriate extension methods for this provider type.");
                }
            });
            return task;
        }

        public Task<string?> DeleteAsync(string? keyOrRelativeUrlPath = null, object? dataObject = null)
        {
            return DeleteAsync<string>(keyOrRelativeUrlPath, dataObject);
        }

        public string? Delete(string? keyOrRelativeUrlPath = null, object? dataObject = null)
        {
            return DeleteAsync(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        #endregion

        #region POST

        public T? Post<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class
        {
            return PostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                .WaitAndGetResult(Configuration.DefaultTimeout);
        }

        /// <summary>
        /// ExpiryInMinutes only works with backend that supports it (such as Redis)
        /// </summary>
        /// <param name="keyOrRelativeUrlPath"></param>
        /// <param name="dataObject"></param>
        /// <param name="expiryInMinutes"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public Task<T?> PostAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class
        {
            var task = Task.Run(() =>
            {
                // Resolve data object from parameters
                var resolvedData = ResolveDataObject<T>(dataObject);

                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        var httpProvider = GetProvider<IHttpProvider>();
                        if (httpProvider == null)
                            throw new InvalidOperationException("HTTP provider not available for HTTP operations.");

                        if (dataObject != null)
                            ObjAsParam = dataObject;
                        return httpProvider.PostAsync<T>(keyOrRelativeUrlPath, ObjAsParam, expiryInMinutes)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Azure:
                    case RestMode.S3:
                    case RestMode.LocalFileSystem:
                        var storageProvider = GetProvider<IStorageProvider>();
                        if (storageProvider == null)
                            throw new InvalidOperationException(
                                $"Storage provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        if (resolvedData == null)
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);

                        return storageProvider.PutAsync<T>(keyOrRelativeUrlPath, resolvedData)
                            .WaitAndGetResult(Configuration.DefaultTimeout);

                    case RestMode.Redis:
                    case RestMode.Memory:
                        var cacheProvider = GetProvider<ICacheProvider>();
                        if (cacheProvider == null)
                            throw new InvalidOperationException(
                                $"Cache provider not available for mode {CurrentMode}. Please reference appropriate provider package.");

                        if (resolvedData == null)
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);

                        var expiry = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                        var success = cacheProvider.SetAsync(keyOrRelativeUrlPath, resolvedData, expiry)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return success ? resolvedData : null;

                    case RestMode.RabbitMq:
                    case RestMode.Kafka:
                    default:
                        throw new NotSupportedException(
                            $"Post operation not supported for mode {CurrentMode}. Use appropriate extension methods for this provider type.");
                }
            });

            return task;
        }

        public Task<string?> PostAsync(string? keyOrRelativeUrlPath = null, string? dataObject = null,
            TimeSpan? expiryInMinutes = null)
        {
            return PostAsync<string>(keyOrRelativeUrlPath, dataObject, expiryInMinutes);
        }

        public string? Post(string? keyOrRelativeUrlPath = null, string? dataObject = null,
            TimeSpan? expiryInMinutes = null)
        {
            return PostAsync(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                .WaitAndGetResult(Configuration.DefaultTimeout);
        }

        #endregion


        #region Private Methods

        public void PrepareHeaders(HttpRequestHeaders headers)
        {
            if (!(Headers.Count > 0)) return;

            foreach (var item in Headers)
            {
                try
                {
                    headers.TryAddWithoutValidation(item.Key, item.Value);
                }
                catch (Exception)
                {
                    //ignore
                }
            }
        }

        public string PrepareInjectParamsIntoQuery(string? urlPath, bool convertObjectAsParam = true)
        {
            urlPath ??= string.Empty;
            var nvc = urlPath.IdentifyQueryParams();
            if (Params.Count > 0)
            {
                foreach (var k in Params.Keys)
                {
                    nvc.Add(k, Params[k]);
                }
            }

            if (convertObjectAsParam && ObjAsParam != null)
            {
                var values = ObjAsParam.GetType().GetProperties()
                    .Where(item => item.GetValue(ObjAsParam, null) != null)
                    .Select(item =>
                        new KeyValuePair<string, string>(item.Name,
                            item.GetValue(ObjAsParam, null)?.ToString() ?? string.Empty));
                var keyValuePairs = values as KeyValuePair<string, string>[] ?? values.ToArray();
                if (keyValuePairs.Any())
                {
                    foreach (var item in keyValuePairs)
                    {
                        if (!nvc.ContainsKey(item.Key))
                        {
                            nvc.Add(item.Key, HttpUtility.UrlEncode(item.Value));
                        }

                        //respect existing parameters so ignore the value from an object
                    }
                }
            }

            var indexOfQuestionMark = urlPath.IndexOf('?');
            if (indexOfQuestionMark > 0)
                return urlPath[..indexOfQuestionMark] + nvc.ParseIntoQueryString();
            return urlPath + nvc.ParseIntoQueryString();
        }

        /// <summary>
        /// Helper method to resolve data object from various parameter sources
        /// </summary>
        private T? ResolveDataObject<T>(object? dataObject) where T : class
        {
            // Priority: dataObject parameter > ObjAsParam > null
            if (dataObject != null)
            {
                if (dataObject is T typedData)
                    return typedData;
                throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
            }

            if (ObjAsParam != null)
            {
                if (ObjAsParam is T typedObjParam)
                    return typedObjParam;
                throw new NotSupportedException(
                    "A object parameter is detected, however it is not same generic type as the return type for the current call.");
            }

            return null;
        }

        /// <summary>
        /// Create HTTP request context from current Rest state
        /// </summary>
        /// <returns>HttpRequestContext with current Rest configuration</returns>
        private HttpRequestContext CreateHttpRequestContext()
        {
            return new HttpRequestContext
            {
                BaseUri = BaseUri,
                Parameters = Params ?? new Dictionary<string, string>(),
                Headers = Headers ?? new Dictionary<string, List<string>>(),
                DataObject = ObjAsParam,
                TimeoutMs = Configuration.DefaultTimeout
            };
        }

        /// <summary>
        /// Check if a cached provider is disposed or unusable
        /// </summary>
        /// <param name="provider">The provider to check</param>
        /// <returns>True if the provider is disposed or unusable, false if still usable</returns>
        private bool IsProviderDisposedOrUnusable(IRestmeProvider provider)
        {
            if (provider == null)
                return true;

            try
            {
                // Test if provider is still usable by accessing a safe property first
                var providerName = provider.ProviderName;
                var capabilities = provider.Capabilities;

                // For providers that implement specific capabilities, try to perform a lightweight operation
                // that would fail if the underlying resources are disposed
                if (provider is IQueueProvider queueProvider)
                {
                    // Try to declare a temporary queue to test if the connection is still alive
                    // This will throw an exception if the provider is disposed
                    var testTask = queueProvider.DeclareQueueAsync($"disposal-test-{Guid.NewGuid():N}",
                        isDurable: false, isExclusive: true, autoDelete: true);

                    // Use a very short timeout to avoid hanging
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    testTask.Wait(cts.Token);
                }
                // Add similar checks for other provider types as needed
                // else if (provider is IStorageProvider storageProvider) { ... }
                // else if (provider is ISearchProvider searchProvider) { ... }

                return false; // Provider is still usable
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            catch (NullReferenceException)
            {
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed") || ex.Message.Contains("closed"))
            {
                return true;
            }
            catch (OperationCanceledException)
            {
                // Timeout occurred, which suggests the provider may be hanging or disposed
                return true;
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Any(inner =>
                                                    inner is ObjectDisposedException ||
                                                    inner is InvalidOperationException ioe &&
                                                    (ioe.Message.Contains("disposed") ||
                                                     ioe.Message.Contains("closed")) ||
                                                    inner is OperationCanceledException))
            {
                return true;
            }
            catch
            {
                // Any other unexpected exception suggests the provider is unusable
                Logger?.LogWarning("Provider {ProviderType} threw unexpected exception during disposal check",
                    provider.GetType().Name);
                return true;
            }
        }

        #endregion


        public void Dispose()
        {
            // Dispose all cached providers
            foreach (var provider in InstantiatedProviders.Values)
            {
                provider?.Dispose();
            }

            InstantiatedProviders.Clear();
        }
    }
}