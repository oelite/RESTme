using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using OElite;
using OElite.Restme.Abstractions;
using OElite.Restme.Utils;

namespace OElite.Restme.Base
{
    /// <summary>
    /// Default HttpClient-based provider implementing full HTTP functionality
    /// that was previously scattered across RestmeHttpExtensions and Rest.
    /// </summary>
    public sealed class HttpClientProvider : IHttpProvider
    {
        private readonly RestConfig _config;
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Provider name for debugging and logging
        /// </summary>
        public string ProviderName => "HttpClient";

        /// <summary>
        /// Configuration used to create this provider
        /// </summary>
        public RestConfig Configuration => _config;

        /// <summary>
        /// Capabilities supported by this provider
        /// </summary>
        public ProviderCapabilities Capabilities => ProviderCapabilities.None;

        public HttpClientProvider(RestConfig config, ILogger? logger = null)
        {
            _config = config;
            _logger = logger;
        }

        #region Context-based methods (primary implementation)

        public async Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
        {
            var response = await HttpRequestRawAsync(method, keyOrRelativePath, context);
            if (response?.IsSuccessStatusCode != true)
            {
                return default(T);
            }

            try
            {
                return await ParseResponseContent<T>(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse response content for type {Type}", typeof(T).Name);
                return default(T);
            }
            finally
            {
                response.Dispose();
            }
        }

        public async Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
        {
            return await HttpRequestRawAsync(method, keyOrRelativePath, context);
        }

        public async Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
        {
            return await HttpRequestFullWithDetailsInternalAsync<T>(method, keyOrRelativePath, context);
        }

        #endregion

        #region Simplified methods (backward compatibility)

        public async Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            var context = new HttpRequestContext
            {
                BaseUri = GetBaseUriFromConfig(),
                DataObject = dataObject,
                TimeoutMs = _config.DefaultTimeout
            };
            return await HttpRequestAsync<T>(method, keyOrRelativePath, context);
        }

        public async Task<HttpResponseMessage?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            var context = new HttpRequestContext
            {
                BaseUri = GetBaseUriFromConfig(),
                DataObject = dataObject,
                TimeoutMs = _config.DefaultTimeout
            };
            return await HttpRequestFullAsync<T>(method, keyOrRelativePath, context);
        }

        public async Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsAsync<T>(HttpMethod method, string? keyOrRelativePath = null, object? dataObject = null)
        {
            var context = new HttpRequestContext
            {
                BaseUri = GetBaseUriFromConfig(),
                DataObject = dataObject,
                TimeoutMs = _config.DefaultTimeout
            };
            return await HttpRequestFullWithDetailsAsync<T>(method, keyOrRelativePath, context);
        }

        public Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Get, keyOrRelativePath, dataObject);
        }

        public Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Post, keyOrRelativePath, dataObject);
        }

        public Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Put, keyOrRelativePath, dataObject);
        }

        public Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null)
        {
            return HttpRequestAsync<T>(HttpMethod.Delete, keyOrRelativePath, null);
        }

        #endregion

        #region Private implementation

        /// <summary>
        /// Perform raw HTTP request and return HttpResponseMessage
        /// </summary>
        private async Task<HttpResponseMessage?> HttpRequestRawAsync(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
        {
            using var httpClient = new HttpClient();

            // Set base address
            if (context.BaseUri != null)
            {
                httpClient.BaseAddress = context.BaseUri;
            }

            // Set timeout
            if (context.TimeoutMs > 0)
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(context.TimeoutMs);
            }

            // Set headers
            PrepareHeaders(httpClient.DefaultRequestHeaders, context.Headers);

            // Prepare request content and path
            HttpContent? content = null;
            var finalPath = keyOrRelativePath ?? "";

            try
            {
                if (context.DataObject != null)
                {
                    if (method == HttpMethod.Get || method == HttpMethod.Delete)
                    {
                        // For GET/DELETE, inject data and parameters into query string
                        finalPath = InjectDataIntoQuery(finalPath, context.DataObject, context.Parameters);
                    }
                    else
                    {
                        // For POST/PUT, create HTTP content from data and parameters
                        content = CreateHttpContent(context.DataObject, context.Parameters);
                    }
                }
                else if (context.Parameters.Count > 0)
                {
                    if (method == HttpMethod.Get || method == HttpMethod.Delete)
                    {
                        // Inject parameters into query string
                        finalPath = InjectParametersIntoQuery(finalPath, context.Parameters);
                    }
                    else
                    {
                        // Create content from parameters
                        content = CreateHttpContent(null, context.Parameters);
                    }
                }

                // Build final request URI
                Uri? requestUri = null;
                if (httpClient.BaseAddress != null)
                {
                    requestUri = string.IsNullOrEmpty(finalPath)
                        ? httpClient.BaseAddress
                        : new Uri(httpClient.BaseAddress, finalPath);
                }
                else if (!string.IsNullOrEmpty(finalPath))
                {
                    requestUri = new Uri(finalPath);
                }

                if (requestUri == null)
                {
                    throw new InvalidOperationException("Unable to determine request URI. Either BaseUri or absolute path must be provided.");
                }

                // Execute HTTP request
                return method.Method switch
                {
                    "GET" => await httpClient.GetAsync(requestUri),
                    "POST" => await httpClient.PostAsync(requestUri, content),
                    "PUT" => await httpClient.PutAsync(requestUri, content),
                    "DELETE" => await httpClient.DeleteAsync(requestUri),
                    _ => throw new NotSupportedException($"HTTP method {method.Method} is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "HTTP request failed for {Method} {Path}", method.Method, keyOrRelativePath);
                throw;
            }
            finally
            {
                content?.Dispose();
            }
        }

        /// <summary>
        /// Parse HTTP response content into the specified type
        /// </summary>
        private async Task<T?> ParseResponseContent<T>(HttpResponseMessage response)
        {
            var contentString = await response.Content.ReadAsStringAsync();

            // Handle Stream types
            if (typeof(T).IsSubclassOf(typeof(Stream)) || typeof(T) == typeof(Stream))
            {
                var stream = await response.Content.ReadAsStreamAsync();
                return (T)(object)stream;
            }

            // Handle string type
            if (typeof(T) == typeof(string))
            {
                return (T)(object)contentString;
            }

            // Handle other types via JSON deserialization
            if (!string.IsNullOrEmpty(contentString))
            {
                return contentString.JsonDeserialize<T>(true, _config.SerializerSettings);
            }

            return default(T);
        }

        /// <summary>
        /// Prepare HTTP request headers
        /// </summary>
        private void PrepareHeaders(System.Net.Http.Headers.HttpRequestHeaders headers, Dictionary<string, List<string>> contextHeaders)
        {
            foreach (var headerPair in contextHeaders)
            {
                try
                {
                    headers.TryAddWithoutValidation(headerPair.Key, headerPair.Value);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to add header {HeaderName}", headerPair.Key);
                }
            }
        }

        /// <summary>
        /// Create appropriate HTTP content based on data object and parameters
        /// </summary>
        private HttpContent CreateHttpContent(object? dataObject, Dictionary<string, string> parameters)
        {
            HttpContent content;

            if (_config.OperationMode == RestMode.Http)
            {
                // Form URL encoded for traditional HTTP mode
                if (dataObject != null)
                {
                    if (dataObject is Dictionary<string, string> dataDict)
                    {
                        // Merge data dict with parameters
                        var combinedParams = new Dictionary<string, string>(parameters);
                        foreach (var kvp in dataDict)
                        {
                            combinedParams[kvp.Key] = kvp.Value;
                        }
                        content = new OEliteFormUrlEncodedContent(combinedParams);
                    }
                    else
                    {
                        content = new StringContent(StringUtils.JsonSerialize(dataObject));
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    }
                }
                else
                {
                    content = new OEliteFormUrlEncodedContent(parameters);
                }
            }
            else
            {
                // JSON content for REST mode
                if (dataObject != null)
                {
                    if (dataObject is Dictionary<string, string> dataDict)
                    {
                        // Merge data dict with parameters
                        var combinedParams = new Dictionary<string, string>(parameters);
                        foreach (var kvp in dataDict)
                        {
                            combinedParams[kvp.Key] = kvp.Value;
                        }
                        content = new OEliteRestfulHttpContent(combinedParams);
                    }
                    else
                    {
                        content = new StringContent(StringUtils.JsonSerialize(dataObject));
                    }
                }
                else
                {
                    content = new OEliteRestfulHttpContent(parameters);
                }
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            return content;
        }

        /// <summary>
        /// Inject data object and parameters into query string for GET/DELETE requests
        /// </summary>
        private string InjectDataIntoQuery(string urlPath, object dataObject, Dictionary<string, string> parameters)
        {
            var nvc = urlPath.IdentifyQueryParams();

            // Add parameters first
            foreach (var kvp in parameters)
            {
                nvc.Add(kvp.Key, kvp.Value);
            }

            // Add data object properties
            if (dataObject is Dictionary<string, string> dataDict)
            {
                foreach (var kvp in dataDict)
                {
                    nvc.Add(kvp.Key, kvp.Value);
                }
            }
            else
            {
                // Convert object properties to query parameters
                var properties = dataObject.GetType().GetProperties();
                foreach (var property in properties)
                {
                    var value = property.GetValue(dataObject, null);
                    if (value != null)
                    {
                        nvc.Add(property.Name, HttpUtility.UrlEncode(value.ToString()));
                    }
                }
            }

            var indexOfQuestionMark = urlPath.IndexOf('?');
            if (indexOfQuestionMark > 0)
                return urlPath[..indexOfQuestionMark] + nvc.ParseIntoQueryString();
            return urlPath + nvc.ParseIntoQueryString();
        }

        /// <summary>
        /// Inject parameters into query string
        /// </summary>
        private string InjectParametersIntoQuery(string urlPath, Dictionary<string, string> parameters)
        {
            var nvc = urlPath.IdentifyQueryParams();

            foreach (var kvp in parameters)
            {
                nvc.Add(kvp.Key, kvp.Value);
            }

            var indexOfQuestionMark = urlPath.IndexOf('?');
            if (indexOfQuestionMark > 0)
                return urlPath[..indexOfQuestionMark] + nvc.ParseIntoQueryString();
            return urlPath + nvc.ParseIntoQueryString();
        }

        /// <summary>
        /// Get base URI from configuration
        /// </summary>
        private Uri? GetBaseUriFromConfig()
        {
            if (!string.IsNullOrEmpty(_config.ConnectionString))
            {
                if (Uri.TryCreate(_config.ConnectionString, UriKind.Absolute, out var uri))
                {
                    return uri;
                }
            }
            return null;
        }

        /// <summary>
        /// Enhanced HTTP request with detailed response information including headers, status codes, and error handling
        /// </summary>
        private async Task<HttpResponseMessage<T>?> HttpRequestFullWithDetailsInternalAsync<T>(HttpMethod method, string? keyOrRelativePath, HttpRequestContext context)
        {
            using var httpClient = new HttpClient();

            // Set base address
            if (context.BaseUri != null)
            {
                httpClient.BaseAddress = context.BaseUri;
            }

            // Set timeout
            if (context.TimeoutMs > 0)
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(context.TimeoutMs);
            }

            // Set headers
            PrepareHeaders(httpClient.DefaultRequestHeaders, context.Headers);

            var result = new HttpResponseMessage<T>();
            result.RequestHeaders = httpClient.DefaultRequestHeaders;

            // Prepare request content and path
            HttpContent? content = null;
            var finalPath = keyOrRelativePath ?? "";

            HttpResponseMessage? response = null;
            try
            {
                if (context.DataObject != null)
                {
                    if (method == HttpMethod.Get || method == HttpMethod.Delete)
                    {
                        // For GET/DELETE, inject data and parameters into query string
                        finalPath = InjectDataIntoQuery(finalPath, context.DataObject, context.Parameters);
                    }
                    else
                    {
                        // For POST/PUT, create HTTP content from data and parameters
                        content = CreateHttpContent(context.DataObject, context.Parameters);
                    }
                }
                else if (context.Parameters.Count > 0)
                {
                    if (method == HttpMethod.Get || method == HttpMethod.Delete)
                    {
                        // Inject parameters into query string
                        finalPath = InjectParametersIntoQuery(finalPath, context.Parameters);
                    }
                    else
                    {
                        // Create content from parameters
                        content = CreateHttpContent(null, context.Parameters);
                    }
                }

                // Build final request URI
                Uri? requestUri = null;
                if (httpClient.BaseAddress != null)
                {
                    requestUri = string.IsNullOrEmpty(finalPath)
                        ? httpClient.BaseAddress
                        : new Uri(httpClient.BaseAddress, finalPath);
                }
                else if (!string.IsNullOrEmpty(finalPath))
                {
                    requestUri = new Uri(finalPath);
                }

                if (requestUri == null)
                {
                    throw new InvalidOperationException("Unable to determine request URI. Either BaseUri or absolute path must be provided.");
                }

                // Execute HTTP request
                response = method.Method switch
                {
                    "GET" => await httpClient.GetAsync(requestUri),
                    "POST" => await httpClient.PostAsync(requestUri, content),
                    "PUT" => await httpClient.PutAsync(requestUri, content),
                    "DELETE" => await httpClient.DeleteAsync(requestUri),
                    _ => throw new NotSupportedException($"HTTP method {method.Method} is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "HTTP request failed for {Method} {Path}", method.Method, keyOrRelativePath);
                result.ErrorMessage = ex;
                result.ReceivedOnUtc = DateTime.UtcNow;
                return result;
            }
            finally
            {
                content?.Dispose();
                result.ReceivedOnUtc = DateTime.UtcNow;
            }

            if (response == null)
            {
                result.ErrorMessage = new InvalidOperationException("HTTP response was null");
                return result;
            }

            // Set response metadata
            result.ResponseHeaders = response.Headers;
            result.StatusCode = response.StatusCode;

            // Read response content as string
            try
            {
                result.DataInString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read response content as string");
                // Continue processing even if string content reading fails
            }

            // Process response content based on type
            try
            {
                if (typeof(T).IsSubclassOf(typeof(Stream)) || typeof(T) == typeof(Stream))
                {
                    // Handle Stream types
                    var streamContent = await response.Content.ReadAsStreamAsync();
                    result.Data = (T)(object)streamContent;
                }
                else if (typeof(T) == typeof(string))
                {
                    // Handle string type directly
                    result.Data = (T)(object)(result.DataInString ?? string.Empty);
                }
                else
                {
                    // Handle other types via JSON deserialization or primitive conversion
                    var contentString = result.DataInString;
                    if (!string.IsNullOrEmpty(contentString))
                    {
                        if (typeof(T).IsPrimitiveType())
                        {
                            // Convert primitive types directly
                            result.Data = (T)Convert.ChangeType(contentString, typeof(T));
                        }
                        else
                        {
                            // JSON deserialize complex types using configuration settings
                            result.Data = contentString.JsonDeserialize<T>(
                                _config.UseRestConvertForCollectionSerialization,
                                _config.SerializerSettings);
                        }
                    }
                    else
                    {
                        result.Data = default(T);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process response content for type {Type}", typeof(T).Name);
                result.ErrorMessage = ex;
                result.Data = default(T);
            }
            finally
            {
                response.Dispose();
            }

            return result;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}