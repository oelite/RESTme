using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeHttpExtensions
    {
        public static T HttpRequest<T>(this Restme restme, HttpMethod method, string relativeUrlPath = null)
        {
            return Task.Run(async () => await restme.HttpRequestAsync<T>(method, relativeUrlPath)).Result;
        }

        public static async Task<T> HttpRequestAsync<T>(this Restme restme, HttpMethod method, string relativePath = null)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = restme.BaseUri;
            restme.PrepareHeaders(httpClient.DefaultRequestHeaders);
            HttpResponseMessage response = null;

            if (method == HttpMethod.Post)
            {
                response = await httpClient.PostAsync(new Uri(restme.BaseUri, relativePath), new OEliteFormUrlEncodedContent(restme._params));
            }
            else if (method == HttpMethod.Put)
            {
                response = await httpClient.PutAsync(new Uri(restme.BaseUri, relativePath), new OEliteFormUrlEncodedContent(restme._params));
            }
            else if (method == HttpMethod.Get)
            {
                response = await httpClient.GetAsync(new Uri(restme.BaseUri, restme.PrepareInjectParamsIntoQuery(relativePath)));
            }
            else if (method == HttpMethod.Delete)
            {
                response = await httpClient.DeleteAsync(new Uri(restme.BaseUri, restme.PrepareInjectParamsIntoQuery(relativePath)));
            }

            var content = await response.Content.ReadAsStringAsync();

            try
            {
                if (typeof(T).IsPrimitiveType())
                {
                    return (T)Convert.ChangeType(content, typeof(T));
                }
                else
                {
                    return JsonConvert.DeserializeObject<T>(content, restme._jsonSerializerSettings);
                }
            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        public static T HttpGet<T>(this Restme restme, string relativeUrlPath = null)
        {
            return Task.Run(async () => await restme.HttpGetAsync<T>(relativeUrlPath)).Result;
        }

        public static async Task<T> HttpGetAsync<T>(this Restme restme, string relativeUrlPath = null)
        {
            return await restme.RequestAsync<T>(HttpMethod.Get, relativeUrlPath);
        }

        public static T HttpPost<T>(this Restme restme, string relativeUrlPath = null)
        {
            return Task.Run(async () => await restme.HttpPostAsync<T>(relativeUrlPath)).Result;
        }

        public static async Task<T> HttpPostAsync<T>(this Restme restme, string relativeUrlPath = null)
        {
            return await restme.RequestAsync<T>(HttpMethod.Post, relativeUrlPath);
        }

    }
}
