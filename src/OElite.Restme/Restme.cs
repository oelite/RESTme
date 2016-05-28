using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.IO;

namespace OElite
{
    public partial class Restme : IRestme
    {
        internal Dictionary<string, string> _params;
        internal Dictionary<string, List<string>> _headers;
        internal JsonSerializerSettings _jsonSerializerSettings;
        internal object _objAsParam;

        public Restme(Uri baseUri = null, string urlPath = null, JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.BaseUri = baseUri;
            this.RequestUrlPath = urlPath;
            _params = new Dictionary<string, string>();
            _headers = new Dictionary<string, List<string>>();
            _jsonSerializerSettings = jsonSerializerSettings ?? new JsonSerializerSettings() { ContractResolver = new OEliteJsonResolver() };
            this.PrepareRestMode();
        }

        public Uri BaseUri { get; set; }
        public string RequestUrlPath { get; set; }
        public RestMode CurrentMode { get; set; }


        public void Add(object value)
        {
            if (_params?.Count > 0)
                throw new InvalidOperationException("Additional parameters have been added, try use Add(string key, object value) instead of Add(object value).");
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
                _params[key] = JsonConvert.SerializeObject(value);
            else
                _params.Add(key, JsonConvert.SerializeObject(value));

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
                    _headers[header] = new List<string>() { value };

            }
            else
                _headers.Add(header, new List<string>() { value });
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
                    throw new NotSupportedException("Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }
        }

        public T Get<T>(string relativeUrlPath = null)
        {
            return Task.Run(async () => await GetAsync<T>(relativeUrlPath)).Result;
        }

        public async Task<T> GetAsync<T>(string relativeUrlPath = null)
        {
            switch (CurrentMode)
            {
                case RestMode.HTTPClient:
                    return await this.HttpGetAsync<T>(relativeUrlPath);
                case RestMode.AzureStorageClient:
                    return await this.StorageGetAsync<T>(relativeUrlPath);
                case RestMode.RedisCacheClient:
                    return await this.RedisGetAsync<T>(relativeUrlPath);
                default:
                    throw new NotSupportedException("Generic request async method only supports HTTP requests, please use other extension methods or switch operation RestMode to HTTPClient");
            }

        }

        public T Post<T>(string relativeUrlPath = null)
        {
            return Task.Run(async () => await PostAsync<T>(relativeUrlPath)).Result;
        }

        public async Task<T> PostAsync<T>(string relativeUrlPath = null)
        {
            return await RequestAsync<T>(HttpMethod.Post, relativeUrlPath);
        }

        #region Private Methods        
        public void PrepareHeaders(HttpRequestHeaders headers)
        {
            if (_headers?.Count > 0)
            {
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
        }
        public string PrepareInjectParamsIntoQuery(string urlPath)
        {
            urlPath = urlPath ?? string.Empty;
            var nvc = urlPath.IdentifyQueryParams();
            if (_params?.Count > 0)
            {
                foreach (var k in _params.Keys)
                {
                    nvc.Set(k, _params[k]);
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
