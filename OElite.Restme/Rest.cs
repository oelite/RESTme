using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using OElite.Abstractions;

namespace OElite
{
    public partial class Rest : IRestme, IDisposable
    {
        internal Dictionary<string, string> Params;
        internal Dictionary<string, List<string>> Headers;
        internal object? ObjAsParam;
        public RestConfig Configuration { get; private set; }

        public Uri? BaseUri { get; set; }
        public string? ConnectionString { get; }
        public string? RequestUrlPath { get; set; }
        public bool Initialized { get; private set; }

        // Provider properties for dynamic loading
        public ICacheProvider? CacheProvider { get; private set; }
        public IQueueProvider? QueueProvider { get; private set; }
        public IStorageProvider? StorageProvider { get; private set; }
        public IHttpProvider? HttpProvider { get; private set; }
        public ILogProvider? LogProvider { get; private set; }


        public Rest(Uri? baseUri = null,
            string? urlPath = null, RestConfig? config = null, ILogger? logger = null,
            Dictionary<string, string>? @params = null, Dictionary<string, List<string>>? headers = null)
        {
            Params = @params ?? new Dictionary<string, string>();
            Headers = headers ?? new Dictionary<string, List<string>>();
            BaseUri = baseUri!;
            RequestUrlPath = urlPath;
            Logger = logger;
            Configuration = config!;
            this.PrepareRestMode();
        }

        public Rest(string? endPointOrConnectionString, RestConfig? configuration = null, ILogger? logger = null,
            Dictionary<string, string>? @params = null, Dictionary<string, List<string>>? headers = null)
        {
            Params = @params ?? new Dictionary<string, string>();
            Headers = headers ?? new Dictionary<string, List<string>>();
            if (endPointOrConnectionString != null && endPointOrConnectionString.StartsWith("http"))
                BaseUri = new Uri(endPointOrConnectionString);
            else
                ConnectionString = endPointOrConnectionString;
            Logger = logger;
            Configuration = configuration!;
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
        /// Initialize providers based on current mode and available assemblies
        /// </summary>
        private void InitializeProviders()
        {
            try
            {
                // Initialize log provider first (always available)
                LogProvider = new OElite.Base.ConsoleLogProvider();

                // Load appropriate assemblies to trigger static constructors
                LoadProviderAssemblies();

                // Try to load providers dynamically based on mode
                switch (Configuration.OperationMode)
                {
                    case RestMode.MemoryAsCache:
                        CacheProvider = new OElite.Base.MemoryCacheProvider();
                        break;
                    case RestMode.LocalFileSystemAsStorage:
                        StorageProvider = new OElite.Base.LocalFileSystemStorageProvider(ConnectionString);
                        break;
                    case RestMode.InMemoryQueue:
                        QueueProvider = new OElite.Base.MemoryQueueProvider();
                        break;
                    case RestMode.RedisAsCache:
                        InitializeCacheProvider();
                        break;
                    case RestMode.RabbitMq:
                        InitializeQueueProvider();
                        break;
                    case RestMode.AzureAsStorage:
                        InitializeStorageProvider();
                        break;
                    case RestMode.AzureAsCache:
                        InitializeCacheProvider();
                        break;
                    case RestMode.S3AsStorage:
                        InitializeStorageProvider();
                        break;
                    case RestMode.S3AsCache:
                        InitializeCacheProvider();
                        break;
                    case RestMode.Http:
                    case RestMode.HttpRest:
                    default:
                        InitializeHttpProvider();
                        break;
                }

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
                switch (Configuration.OperationMode)
                {
                    case RestMode.RedisAsCache:
                        LoadAssembly("OElite.Restme.Redis");
                        break;
                    case RestMode.RabbitMq:
                        LoadAssembly("OElite.Restme.RabbitMQ");
                        break;
                    case RestMode.AzureAsStorage:
                    case RestMode.AzureAsCache:
                        LoadAssembly("OElite.Restme.Azure");
                        break;
                    case RestMode.S3AsStorage:
                    case RestMode.S3AsCache:
                        LoadAssembly("OElite.Restme.S3");
                        break;
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
        private void LoadAssembly(string assemblyName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.Load(assemblyName);
                // Force static constructors to run by accessing a type
                var types = assembly.GetTypes();
            }
            catch (System.IO.FileNotFoundException)
            {
                // Assembly not found - this is expected if the backend package isn't referenced
                Logger?.LogDebug("Provider assembly {AssemblyName} not found - backend package may not be referenced",
                    assemblyName);
            }
        }

        /// <summary>
        /// Initialize cache provider (Redis, Azure, or S3)
        /// </summary>
        private void InitializeCacheProvider()
        {
            try
            {
                string factoryName = Configuration.OperationMode switch
                {
                    RestMode.RedisAsCache => "redis",
                    RestMode.AzureAsCache => "azure",
                    RestMode.S3AsCache => "s3",
                    _ => "redis" // Default fallback
                };

                var factory = ServiceLocator.GetFactory(factoryName);
                if (factory != null)
                {
                    CacheProvider = factory.CreateCacheProvider(ConnectionString ?? "", Configuration);
                }
                else
                {
                    // Fallback to default implementation that throws helpful error
                    CacheProvider = new DefaultCacheProvider();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize cache provider");
                var packageName = Configuration.OperationMode switch
                {
                    RestMode.RedisAsCache => "OElite.Restme.Redis",
                    RestMode.AzureAsCache => "OElite.Restme.Azure",
                    RestMode.S3AsCache => "OElite.Restme.S3",
                    _ => "OElite.Restme.Redis"
                };
                throw new OEliteException(
                    $"{Configuration.OperationMode} cache provider not loaded. Please reference {packageName} package.",
                    ex);
            }
        }

        /// <summary>
        /// Initialize queue provider (RabbitMQ)
        /// </summary>
        private void InitializeQueueProvider()
        {
            try
            {
                // Try to load RabbitMQ provider dynamically
                var factory = ServiceLocator.GetFactory("rabbitmq");
                if (factory != null)
                {
                    // For RabbitMQ, include VHost in connection string if provided
                    var connectionStringWithVHost = ConnectionString ?? "";
                    if (!string.IsNullOrEmpty(RequestUrlPath))
                    {
                        // Append VHost info to connection string for RabbitMQ
                        connectionStringWithVHost += $"|vhost={RequestUrlPath}";
                    }

                    QueueProvider = factory.CreateQueueProvider(connectionStringWithVHost, Configuration);
                }
                else
                {
                    // Fallback to default implementation that throws helpful error
                    QueueProvider = new DefaultQueueProvider();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize queue provider");
                throw new OEliteException(
                    "RabbitMQ provider not loaded. Please reference OElite.Restme.RabbitMQ package.", ex);
            }
        }

        /// <summary>
        /// Initialize storage provider (Azure or S3)
        /// </summary>
        private void InitializeStorageProvider()
        {
            try
            {
                // Try to load storage provider dynamically based on mode
                string factoryName = Configuration.OperationMode switch
                {
                    RestMode.AzureAsStorage => "azure",
                    RestMode.S3AsStorage => "s3",
                    _ => "azure"
                };
                var factory = ServiceLocator.GetFactory(factoryName);
                if (factory != null)
                {
                    StorageProvider = factory.CreateStorageProvider(ConnectionString ?? "", Configuration);
                }
                else
                {
                    // Fallback to default implementation that throws helpful error
                    StorageProvider = new DefaultStorageProvider();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize storage provider");
                var packageName = Configuration.OperationMode switch
                {
                    RestMode.AzureAsStorage => "OElite.Restme.Azure",
                    RestMode.S3AsStorage => "OElite.Restme.S3",
                    _ => "OElite.Restme.Azure"
                };
                throw new OEliteException(
                    $"{Configuration.OperationMode} provider not loaded. Please reference {packageName} package.", ex);
            }
        }

        /// <summary>
        /// Initialize HTTP provider
        /// </summary>
        private void InitializeHttpProvider()
        {
            try
            {
                // Try to load HTTP provider dynamically
                var factory = ServiceLocator.GetFactory<IHttpProvider>();
                if (factory != null)
                {
                    HttpProvider = factory.CreateHttpProvider(Configuration);
                }
                else
                {
                    // Fallback to base implementation without external deps
                    HttpProvider = new OElite.Base.HttpClientProvider(Configuration, Logger);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to initialize HTTP provider");
                // HTTP provider should always be available as fallback
                HttpProvider = new OElite.Base.HttpClientProvider(Configuration, Logger);
            }
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

        public T? HttpRequest<T>(HttpMethod method, string? relativeUrlPath = null)
        {
            return HttpRequestAsync<T>(method, relativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> HttpRequestAsync<T>(HttpMethod method, string? relativePath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.Http:
                case RestMode.HttpRest:
                    return Task.Run(() =>
                        RestmeHttpExtensions.HttpRequestAsync<T>(this, method, relativePath)
                            .WaitAndGetResult(Configuration.DefaultTimeout));
                case RestMode.AzureAsStorage:
                case RestMode.AzureAsCache:
                case RestMode.RedisAsCache:
                default:
                    throw new NotSupportedException(
                        "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
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
                        return this.HttpGetAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureAsStorage:
                    case RestMode.S3AsStorage:
                        if (StorageProvider != null)
                        {
                            return StorageProvider.GetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        throw new InvalidOperationException(
                            "Storage provider not initialized. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
                    case RestMode.RedisAsCache:
                        if (CacheProvider != null)
                        {
                            return CacheProvider.GetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        throw new InvalidOperationException(
                            "Cache provider not initialized. Please reference OElite.Restme.Redis package.");
                    case RestMode.RabbitMq:
                    default:
                        throw new NotSupportedException(
                            "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
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
                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        return HttpRequestAsync<T>(HttpMethod.Put, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureAsStorage:
                    case RestMode.S3AsStorage:
                        if (StorageProvider == null)
                            throw new InvalidOperationException(
                                "Storage provider not initialized. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");

                        if (dataObject != null)
                        {
                            if (dataObject is T typedData)
                                return StorageProvider.PutAsync<T>(keyOrRelativeUrlPath, typedData)
                                    .WaitAndGetResult(Configuration.DefaultTimeout);
                            throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                        }

                        if (ObjAsParam == null)
                        {
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        if (ObjAsParam is T)
                        {
                            dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        if (dataObject is T typedData4)
                            return StorageProvider.PutAsync<T>(keyOrRelativeUrlPath, typedData4)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                    case RestMode.RedisAsCache:
                        if (CacheProvider == null)
                            throw new InvalidOperationException(
                                "Cache provider not initialized. Please reference OElite.Restme.Redis package.");

                        if (dataObject != null)
                        {
                            var expiry = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                            if (dataObject is T typedData)
                            {
                                var success = CacheProvider.SetAsync(keyOrRelativeUrlPath, typedData, expiry)
                                    .WaitAndGetResult(Configuration.DefaultTimeout);
                                return success ? typedData : null;
                            }

                            throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                        }

                        if (ObjAsParam == null)
                        {
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        if (ObjAsParam is T)
                        {
                            dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        var expiry2 = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                        if (dataObject is T typedData5)
                        {
                            var success2 = CacheProvider.SetAsync(keyOrRelativeUrlPath, typedData5, expiry2)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                            return success2 ? typedData5 : null;
                        }

                        throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                    case RestMode.RabbitMq:
                    default:
                        throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
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
                        return HttpRequestAsync<T>(HttpMethod.Delete, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureAsStorage:
                    case RestMode.S3AsStorage:
                        if (StorageProvider == null)
                            throw new InvalidOperationException(
                                "Storage provider not initialized. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
                        var deleteSuccess = StorageProvider.DeleteAsync(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return deleteSuccess ? null : null;
                    case RestMode.RedisAsCache:
                        if (CacheProvider == null)
                            throw new InvalidOperationException(
                                "Cache provider not initialized. Please reference OElite.Restme.Redis package.");
                        var cacheDeleteSuccess = CacheProvider.RemoveAsync(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                        return cacheDeleteSuccess ? null : null;
                    default:
                        throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
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
                switch (CurrentMode)
                {
                    case RestMode.Http:
                    case RestMode.HttpRest:
                        if (dataObject != null)
                            ObjAsParam = dataObject;
                        return HttpRequestAsync<T>(HttpMethod.Post, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureAsStorage:
                    case RestMode.S3AsStorage:
                        if (StorageProvider == null)
                            throw new InvalidOperationException(
                                "Storage provider not initialized. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");

                        if (dataObject != null)
                        {
                            if (dataObject is T typedData)
                                return StorageProvider.PutAsync<T>(keyOrRelativeUrlPath, typedData)
                                    .WaitAndGetResult(Configuration.DefaultTimeout);
                            throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                        }

                        if (ObjAsParam == null)
                        {
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        if (ObjAsParam is T)
                        {
                            dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        if (dataObject is T typedData4)
                            return StorageProvider.PutAsync<T>(keyOrRelativeUrlPath, typedData4)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                    case RestMode.RedisAsCache:
                        if (CacheProvider == null)
                            throw new InvalidOperationException(
                                "Cache provider not initialized. Please reference OElite.Restme.Redis package.");

                        if (dataObject != null)
                        {
                            var expiry = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                            if (dataObject is T typedData)
                            {
                                var success = CacheProvider.SetAsync(keyOrRelativeUrlPath, typedData, expiry)
                                    .WaitAndGetResult(Configuration.DefaultTimeout);
                                return success ? typedData : null;
                            }

                            throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                        }

                        switch (ObjAsParam)
                        {
                            case null:
                                return DeleteAsync<T>(keyOrRelativeUrlPath)
                                    .WaitAndGetResult(Configuration.DefaultTimeout);
                            case T:
                                dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                                break;
                            default:
                                throw new NotSupportedException(
                                    "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        var expiry2 = expiryInMinutes?.TotalMinutes > 0 ? expiryInMinutes : null;
                        if (dataObject is T typedData6)
                        {
                            var success2 = CacheProvider.SetAsync(keyOrRelativeUrlPath, typedData6, expiry2)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                            return success2 ? typedData6 : null;
                        }

                        throw new InvalidOperationException($"Data object is not of type {typeof(T).Name}");
                    case RestMode.RabbitMq:
                    default:
                        throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
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

        #endregion


        public void Dispose()
        {
            try
            {
                // Dispose providers
                CacheProvider?.Dispose();
                QueueProvider?.Dispose();
                StorageProvider?.Dispose();
                HttpProvider?.Dispose();
                LogProvider?.Dispose();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error disposing providers");
            }
        }
    }
}