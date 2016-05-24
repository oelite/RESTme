using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using OElite.Restme.Utils;

namespace OElite.Restme
{
    public class Restme : IRestme
    {
        private JsonSerializerSettings _jsonSettings = null;
        public Restme(Uri baseUri = null, string urlPath = null, JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.BaseUri = baseUri;
            this.RequestUrlPath = urlPath;
            _params = new Dictionary<string, string>();
            _headers = new Dictionary<string, List<string>>();
            _jsonSettings = jsonSerializerSettings ?? new JsonSerializerSettings() { ContractResolver = new OEliteJsonResolver() };
        }
        private Dictionary<string, string> _params;
        private Dictionary<string, List<string>> _headers;
        private object _objAsParam;

        public Uri BaseUri { get; set; }
        public string RequestUrlPath { get; set; }



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
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = BaseUri;
            PrepareHeaders(httpClient.DefaultRequestHeaders);
            HttpResponseMessage response = null;

            if (method == HttpMethod.Post)
            {
                response = await httpClient.PostAsync(new Uri(BaseUri, relativePath), new OEliteFormUrlEncodedContent(_params));
            }
            else if (method == HttpMethod.Put)
            {
                response = await httpClient.PutAsync(new Uri(BaseUri, relativePath), new OEliteFormUrlEncodedContent(_params));
            }
            else if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(new Uri(BaseUri, PrepareInjectParamsIntoQuery(relativePath)));
            }
            else if (method == HttpMethod.Delete)
            {
                response = await httpClient.DeleteAsync(new Uri(BaseUri, PrepareInjectParamsIntoQuery(relativePath)));
            }

            var content = await response.Content.ReadAsStringAsync();

            try
            {
                if (typeof(T).IsConvertable())
                {
                    return (T)Convert.ChangeType(content, typeof(T));
                }
                else
                {
                    return JsonConvert.DeserializeObject<T>(content, _jsonSettings);
                }
            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        public T Get<T>(string relativeUrlPath = null)
        {
            return Task.Run(async () => await GetAsync<T>(relativeUrlPath)).Result;
        }

        public async Task<T> GetAsync<T>(string relativeUrlPath = null)
        {
            return await RequestAsync<T>(HttpMethod.Get, relativeUrlPath);
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
        private void PrepareHeaders(HttpRequestHeaders headers)
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
        private string PrepareInjectParamsIntoQuery(string urlPath)
        {
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
