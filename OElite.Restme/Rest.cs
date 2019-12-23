using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OElite
{
    public partial class Rest : IRestme, IDisposable
    {
        internal Dictionary<string, string> _params;
        internal Dictionary<string, List<string>> _headers;
        internal object _objAsParam;
        public RestConfig Configuration { get; set; }

        public Uri BaseUri { get; set; }
        public string ConnectionString { get; set; }
        public string RequestUrlPath { get; set; }
        public bool Initialized { get; set; }


        private void Init(RestConfig config = null)
        {
            _params = new Dictionary<string, string>();
            _headers = new Dictionary<string, List<string>>();

            Configuration = config ?? new RestConfig();
            this.PrepareRestMode();
        }

        public Rest(Uri baseUri = null, string urlPath = null, RestConfig config = null, ILogger logger = null)
        {
            BaseUri = baseUri;
            RequestUrlPath = urlPath;
            Logger = logger;
            Init(config);
        }

        public Rest(string endPointOrConnectionString, RestConfig configuration = null, ILogger logger = null)
        {
            var lowerConn = endPointOrConnectionString;
            if (lowerConn != null && lowerConn.StartsWith("http"))
                BaseUri = new Uri(endPointOrConnectionString);
            else
                ConnectionString = endPointOrConnectionString;
            Logger = logger;
            Init(configuration);
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
                        PrepareStorageRestme();
                        break;
                    case RestMode.RedisCacheClient:
                        PrepareRedisRestme();
                        break;
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                    default:
                        PrepareHttpRestme();
                        break;
                }
            }
        }


        public void Add(object value)
        {
            if (_params?.Count > 0)
                throw new InvalidOperationException(
                    "Additional parameters have been added, try use Add(string key, object value) instead of Add(object value).");
            _objAsParam = value;
        }

        public void Add(string key, string value)
        {
            _params = _params ?? new Dictionary<string, string>();

            if (_params.ContainsKey(key))
                _params[key] = value;
            else
                _params.Add(key, value);
        }

        public void Add(string key, object value)
        {
            _params = _params ?? new Dictionary<string, string>();

            if (_params.ContainsKey(key))
                _params[key] = value.JsonSerialize(Configuration.UseRestConvertForCollectionSerialization,
                    Configuration.SerializerSettings);
            else
                _params.Add(key,
                    value.JsonSerialize(Configuration.UseRestConvertForCollectionSerialization,
                        Configuration.SerializerSettings));
        }

        public void AddHeader(string header, string value, bool allowMultipleValues = false)
        {
            _headers = _headers ?? new Dictionary<string, List<string>>();
            if (_headers.ContainsKey(header))
            {
                _headers[header] = _headers[header] ?? new List<string>();
                if (allowMultipleValues)
                    _headers[header].Add(value);
                else
                    _headers[header] = new List<string> {value};
            }
            else
                _headers.Add(header, new List<string> {value});
        }

        public void AddBearerToken(string token, bool addBearerPrefix = true)
        {
            AddHeader("Authorization", $"{(addBearerPrefix ? "Bearer " : "")}{token}");
        }

        public T Request<T>(HttpMethod method, string relativeUrlPath = null)
        {
            return RequestAsync<T>(method, relativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T> RequestAsync<T>(HttpMethod method, string relativePath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                case RestMode.HTTPRestClient:
                    return Task.Run(() =>
                        this.HttpRequestAsync<T>(method, relativePath).WaitAndGetResult(Configuration.DefaultTimeout));
                case RestMode.AzureStorageClient:
                case RestMode.RedisCacheClient:
                default:
                    throw new NotSupportedException(
                        "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
        }

        public T Get<T>(string keyOrRelativeUrlPath = null)
        {
            return GetAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T> GetAsync<T>(string keyOrRelativeUrlPath = null)
        {
            var task = Task.Run(() =>
            {
                if (keyOrRelativeUrlPath.IsNotNullOrEmpty())
                {
                    switch (CurrentMode)
                    {
                        case RestMode.HTTPClient:
                        case RestMode.HTTPRestClient:
                            return this.HttpGetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        case RestMode.AzureStorageClient:
                            return this.StorageGetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        case RestMode.RedisCacheClient:
                            return this.RedisGetAsync<T>(keyOrRelativeUrlPath)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        default:
                            throw new NotSupportedException(
                                "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
                    }
                }

                throw new SyntaxErrorException("No key or relative url path provided.");
            });
            return task;
        }

        public string Get(string keyOrRelativePath = null)
        {
            return GetAsync(keyOrRelativePath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<string> GetAsync(string keyOrRelativePath = null)
        {
            return GetAsync<string>(keyOrRelativePath);
        }


        public T Post<T>(string keyOrRelativeUrlPath = null, object dataObject = null)
        {
            return PostAsync<T>(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T> PostAsync<T>(string keyOrRelativeUrlPath = null, object dataObject = null)
        {
            var task = Task.Run<T>(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        if (dataObject != null)
                            _objAsParam = dataObject;
                        return RequestAsync<T>(HttpMethod.Post, keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.AzureStorageClient:
                        if (dataObject != null)
                            return this.StoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        if (_objAsParam == null)
                        {
                            DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                            return default(T);
                        }
                        else if (_objAsParam.GetType() is T)
                        {
                            dataObject = (T) Convert.ChangeType(_objAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        return this.StoragePostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        if (dataObject != null)
                            return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject)
                                .WaitAndGetResult(Configuration.DefaultTimeout);
                        if (_objAsParam == null)
                        {
                            DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
                            return default(T);
                        }
                        else if (_objAsParam is T)
                        {
                            dataObject = (T) Convert.ChangeType(_objAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }

                        return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    default:
                        throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
                }
            });

            return task;
        }

        public Task<string> PostAsync(string keyOrRelativeUrlPath = null, string dataObject = null)
        {
            return PostAsync<string>(keyOrRelativeUrlPath, dataObject);
        }

        public string Post(string keyOrRelativeUrlPath = null, string dataObject = null)
        {
            return PostAsync(keyOrRelativeUrlPath, dataObject).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public T Delete<T>(string keyOrRelativeUrlPath = null)
        {
            return DeleteAsync<T>(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        public Task<T> DeleteAsync<T>(string keyOrRelativeUrlPath = null)
        {
            var task = Task.Run(() =>
            {
                switch (CurrentMode)
                {
                    case RestMode.HTTPClient:
                    case RestMode.HTTPRestClient:
                        return Request<T>(HttpMethod.Delete, keyOrRelativeUrlPath);
                    case RestMode.AzureStorageClient:
                        return this.StorageDeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    case RestMode.RedisCacheClient:
                        return this.RedisDeleteAsync<T>(keyOrRelativeUrlPath)
                            .WaitAndGetResult(Configuration.DefaultTimeout);
                    default:
                        throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
                }
            });
            return task;
        }

        public Task<bool> DeleteAsync(string keyOrRelativeUrlPath = null)
        {
            return DeleteAsync<bool>(keyOrRelativeUrlPath);
        }

        public bool Delete(string keyOrRelativeUrlPath = null)
        {
            return DeleteAsync(keyOrRelativeUrlPath).WaitAndGetResult(Configuration.DefaultTimeout);
        }

        #region Private Methods

        public void PrepareHeaders(HttpRequestHeaders headers)
        {
            if (!(_headers?.Count > 0)) return;

            foreach (var item in _headers)
            {
                try
                {
                    headers.Add(item.Key, item.Value);
                }
                catch (Exception)
                {
                    //ignore
                }
            }
        }

        public string PrepareInjectParamsIntoQuery(string urlPath)
        {
            urlPath = urlPath ?? string.Empty;
            var nvc = urlPath.IdentifyQueryParams();
            if (_params?.Count > 0)
            {
                foreach (var k in _params.Keys)
                {
                    nvc.Add(k, _params[k]);
                }
            }

            var indexOfQuestionMark = urlPath.IndexOf('?');
            if (indexOfQuestionMark > 0)
                return urlPath.Substring(0, indexOfQuestionMark) + nvc.ParseIntoQueryString();
            return urlPath + nvc.ParseIntoQueryString();
        }

        #endregion


        public void Dispose()
        {
            var disposeTasks = new List<Task> {AttemptDisposeRedis()};

            Task.WaitAll(disposeTasks.ToArray());
        }
    }
}