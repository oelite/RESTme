using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace OElite
{
    public partial class Rest : IRestme
    {
        internal Dictionary<string, string> _params;
        internal Dictionary<string, List<string>> _headers;
        internal object _objAsParam;
        public RestConfig Configuration { get; set; }

        public Uri BaseUri { get; set; }
        public string ConnectionString { get; set; }
        public string RequestUrlPath { get; set; }


        private void Init(RestConfig config = null)
        {
            _params = new Dictionary<string, string>();
            _headers = new Dictionary<string, List<string>>();

            this.Configuration = config ?? new RestConfig();
            this.PrepareRestMode();
        }

        public Rest(Uri baseUri = null, string urlPath = null, RestConfig config = null)
        {
            this.BaseUri = baseUri;
            this.RequestUrlPath = urlPath;
            Init(config);
        }

        public Rest(string endPointOrConnectionString, RestConfig configuration = null)
        {
            var lowerConn = endPointOrConnectionString?.ToLower();
            if (lowerConn != null && lowerConn.StartsWith("http"))
                this.BaseUri = new Uri(endPointOrConnectionString);
            else
                ConnectionString = endPointOrConnectionString;
            Init(configuration);
        }


        public RestMode CurrentMode
        {
            get { return Configuration.OperationMode; }
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
            else
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
                    _headers[header] = new List<string>() {value};
            }
            else
                _headers.Add(header, new List<string>() {value});
        }

        public void AddBearerToken(string token)
        {
            AddHeader("Authorization", $"Bearer {token}");
        }

        public T Request<T>(HttpMethod method, string relativeUrlPath = null)
        {
            return Task.Run(async () => await RequestAsync<T>(method, relativeUrlPath)).Result;
        }

        public async Task<T> RequestAsync<T>(HttpMethod method, string relativePath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                    return await this.HttpRequestAsync<T>(method, relativePath);
                case RestMode.AzureStorageClient:
                case RestMode.RedisCacheClient:
                default:
                    throw new NotSupportedException(
                        "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
        }

        public T Get<T>(string keyOrRelativeUrlPath = null)
        {
            return Task.Run(async () => await GetAsync<T>(keyOrRelativeUrlPath)).Result;
        }

        public async Task<T> GetAsync<T>(string keyOrRelativeUrlPath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                    return await this.HttpGetAsync<T>(keyOrRelativeUrlPath);
                case RestMode.AzureStorageClient:
                    return await this.StorageGetAsync<T>(keyOrRelativeUrlPath);
                case RestMode.RedisCacheClient:
                    return await this.RedisGetAsync<T>(keyOrRelativeUrlPath);
                default:
                    throw new NotSupportedException(
                        "Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
        }

        public string Get(string keyOrRelativePath = null)
        {
            return Task.Run(async () => await GetAsync(keyOrRelativePath)).Result;
        }

        public async Task<string> GetAsync(string keyOrRelativePath = null)
        {
            return await GetAsync<string>(keyOrRelativePath);
        }


        public T Post<T>(string keyOrRelativeUrlPath = null, T dataObject = default(T))
        {
            return PostAsync<T>(keyOrRelativeUrlPath, dataObject).Result;
        }

        public Task<T> PostAsync<T>(string keyOrRelativeUrlPath = null, T dataObject = default(T))
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                    if (dataObject != null)
                        throw new NotSupportedException(
                            "dataObject is not expected when using Http Client, please check your RestMode");
                    return RequestAsync<T>(HttpMethod.Post, keyOrRelativeUrlPath);
                case RestMode.AzureStorageClient:
                    if (dataObject == null)
                    {
                        if (_objAsParam == null)
                            throw new NotSupportedException(
                                "dataObject should not be null - use DeleteAsync() method if you intended to delete. Alternatively add an object parameter before the call.");
                        else if (_objAsParam.GetType() is T)
                        {
                            dataObject = (T) Convert.ChangeType(_objAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }
                    }
                    return this.StoragePostAsync<T>(keyOrRelativeUrlPath, dataObject);
                case RestMode.RedisCacheClient:
                    if (dataObject == null)
                    {
                        if (_objAsParam == null)
                            throw new NotSupportedException(
                                "dataObject should not be null - use DeleteAsync() method if you intended to delete. Alternatively add an object parameter before the call.");
                        else if (_objAsParam is T)
                        {
                            dataObject = (T) Convert.ChangeType(_objAsParam, typeof(T));
                        }
                        else
                        {
                            throw new NotSupportedException(
                                "A object parameter is detected, however it is not same generic type as the return type for the current call.");
                        }
                    }
                    return this.RedisPostAsync<T>(keyOrRelativeUrlPath, dataObject);
                default:
                    throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
            }
        }

        public Task<string> PostAsync(string keyOrRelativeUrlPath = null, string dataObject = null)
        {
            return PostAsync<string>(keyOrRelativeUrlPath, dataObject);
        }

        public string Post(string keyOrRelativeUrlPath = null, string dataObject = null)
        {
            return PostAsync(keyOrRelativeUrlPath, dataObject).Result;
        }

        public T Delete<T>(string keyOrRelativeUrlPath = null)
        {
            return Task.Run(async () => await DeleteAsync<T>(keyOrRelativeUrlPath)).Result;
        }

        public Task<T> DeleteAsync<T>(string keyOrRelativeUrlPath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                    return RequestAsync<T>(HttpMethod.Delete, keyOrRelativeUrlPath);
                case RestMode.AzureStorageClient:
                    return this.StorageDeleteAsync<T>(keyOrRelativeUrlPath);
                case RestMode.RedisCacheClient:
                    return this.RedisDeleteAsync<T>(keyOrRelativeUrlPath);
                default:
                    throw new NotSupportedException("Unexpected RestMode, let me call it a break!");
            }
        }

        public Task<bool> DeleteAsync(string keyOrRelativeUrlPath = null)
        {
            return DeleteAsync<bool>(keyOrRelativeUrlPath);
        }

        public bool Delete(string keyOrRelativeUrlPath = null)
        {
            return DeleteAsync(keyOrRelativeUrlPath).Result;
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
            else
                return urlPath + nvc.ParseIntoQueryString();
        }

        #endregion
    }
}