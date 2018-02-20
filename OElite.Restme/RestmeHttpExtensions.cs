﻿using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OElite
{
    public static class RestmeHttpExtensions
    {
        public static T HttpRequest<T>(this Rest restme, HttpMethod method, string relativeUrlPath = null)
        {
            return restme.HttpRequestAsync<T>(method, relativeUrlPath)
                .WaitAndGetResult(restme.Configuration.DefaultTimeout);
        }

        public static async Task<T> HttpRequestAsync<T>(this Rest restme, HttpMethod method, string relativePath = null)
        {
            using (var httpClient = new HttpClient {BaseAddress = restme.BaseUri})
            {
                restme.PrepareHeaders(httpClient.DefaultRequestHeaders);
                HttpResponseMessage response = null;
                ByteArrayContent submitContent = null;
                if (restme.CurrentMode == RestMode.HTTPClient)
                {
                    if (restme._params?.Count > 0)
                        submitContent = new OEliteFormUrlEncodedContent(restme._params);
                    else if (restme?._objAsParam != null)
                    {
                        submitContent = new StringContent(restme._objAsParam.JsonSerialize());
                        submitContent.Headers.ContentType =
                            new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    }
                }
                else
                {
                    if (restme._params?.Count > 0)
                        submitContent = new OEliteRestfulHttpContent(restme._params);
                    else if (restme._objAsParam != null)
                    {
                        submitContent = new StringContent(restme._objAsParam.JsonSerialize());
                    }
                    else
                        submitContent = new StringContent(string.Empty);

                    submitContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                }

                if (method == HttpMethod.Post)
                {
                    response =
                        await
                            httpClient.PostAsync(new Uri(restme.BaseUri, relativePath),
                                submitContent);
                }
                else if (method == HttpMethod.Put)
                {
                    response =
                        await
                            httpClient.PutAsync(new Uri(restme.BaseUri, relativePath),
                                submitContent);
                }
                else if (method == HttpMethod.Get)
                {
                    response =
                        await
                            httpClient.GetAsync(new Uri(restme.BaseUri,
                                restme.PrepareInjectParamsIntoQuery(relativePath)));
                }
                else if (method == HttpMethod.Delete)
                {
                    response =
                        await
                            httpClient.DeleteAsync(new Uri(restme.BaseUri,
                                restme.PrepareInjectParamsIntoQuery(relativePath)));
                }

                if (response == null) return default(T);

                var content = await response.Content.ReadAsStringAsync();
                try
                {
                    if (typeof(T).IsPrimitiveType())
                    {
                        return (T) Convert.ChangeType(content, typeof(T));
                    }
                    else
                    {
                        return content.JsonDeserialize<T>(restme.Configuration.UseRestConvertForCollectionSerialization,
                            restme.Configuration.SerializerSettings);
                    }
                }
                catch (Exception ex)
                {
                    restme?.LogError(ex.Message, ex);
                    return default(T);
                }
            }
        }

        public static T HttpGet<T>(this Rest restme, string relativeUrlPath = null)
        {
            return restme.HttpGetAsync<T>(relativeUrlPath).WaitAndGetResult(restme.Configuration.DefaultTimeout);
        }

        public static Task<T> HttpGetAsync<T>(this Rest restme, string relativeUrlPath = null)
        {
            return restme.RequestAsync<T>(HttpMethod.Get, relativeUrlPath);
        }

        public static T HttpPost<T>(this Rest restme, string relativeUrlPath = null)
        {
            return restme.HttpPostAsync<T>(relativeUrlPath).WaitAndGetResult(restme.Configuration.DefaultTimeout);
        }

        public static Task<T> HttpPostAsync<T>(this Rest restme, string relativeUrlPath = null)
        {
            return restme.RequestAsync<T>(HttpMethod.Post, relativeUrlPath);
        }
    }
}