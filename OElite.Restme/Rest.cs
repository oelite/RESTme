using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;

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
                switch (value)
                {
                    case RestMode.AzureStorageClient:
                        PrepareAzureStorageRestme();
                        break;
                    case RestMode.S3Client:
                        PrepareS3StorageRestme();
                        break;
                    case RestMode.RedisCacheClient:
                        PrepareRedisRestme();
                        break;
                    case RestMode.RabbitMq:
                        PrepareRabbitRestme();
                        break;
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                    default:
                        PrepareHttpRestme();
                        break;
                }
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
                case RestMode.HTTPClient:
                case RestMode.HTTPRestClient:
                    return Task.Run(() =>
                        RestmeHttpExtensions.HttpRequestAsync<T>(this, method, relativePath)
                            .WaitAndGetResult(Configuration.DefaultTimeout));
                case RestMode.AzureStorageClient:
                case RestMode.RedisCacheClient:
                default:
                    throw new NotSupportedException(
                        "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
        }

        #region GET

        public T? Get<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null)
        {
            return GetAsync<T>(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> GetAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null)
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
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        return this.HttpGetAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureStorageClient:
                        return this.AzureStorageGetAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        return this.RedisGetAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.S3Client:
                        return this.S3GetAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
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
            TimeSpan? expiryInMinutes = null)
        {
            return PostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                .WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> PutAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null)
        {
            if (dataObject != null)
                ObjAsParam = dataObject;
            var task = Task.Run(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        return HttpRequestAsync<T>(HttpMethod.Put, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureStorageClient:
                        if (dataObject != null)
                            return this.AzureStoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        if (ObjAsParam == null)
                        {
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        if (ObjAsParam.GetType() is T)
                        {
                            dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        return this.AzureStoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        if (dataObject != null)
                            return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
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

                        return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.S3Client:
                        if (dataObject != null)
                            return this.S3PostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
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

                        return this.S3PostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
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

        public T? Delete<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null)
        {
            return DeleteAsync<T>(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T?> DeleteAsync<T>(string? keyOrRelativeUrlPath = null, object? dataObject = null)
        {
            if (dataObject != null)
                ObjAsParam = dataObject;
            var task = Task.Run(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        return HttpRequestAsync<T>(HttpMethod.Delete, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureStorageClient:
                        return this.AzureStorageDeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        return this.RedisDeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.S3Client:
                        return this.S3DeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
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
            TimeSpan? expiryInMinutes = null)
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
            TimeSpan? expiryInMinutes = null)
        {
            var task = Task.Run(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        if (dataObject != null)
                            ObjAsParam = dataObject;
                        return HttpRequestAsync<T>(HttpMethod.Post, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureStorageClient:
                        if (dataObject != null)
                            return this.AzureStoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        if (ObjAsParam == null)
                        {
                            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                        }

                        if (ObjAsParam.GetType() is T)
                        {
                            dataObject = (T)Convert.ChangeType(ObjAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        return this.AzureStoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        if (dataObject != null)
                            return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
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

                        return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject, expiryInMinutes)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.S3Client:
                        if (dataObject != null)
                            return this.S3PostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
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

                        return this.S3PostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
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
            var disposeTasks = new List<Task> { AttemptDisposeRedis() };

            Task.WaitAll(disposeTasks.ToArray());
        }
    }
}