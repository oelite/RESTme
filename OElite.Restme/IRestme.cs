using System;
using System.Net.Http;
using System.Threading.Tasks;
using OElite.Abstractions;
using OElite.Restme.Utils;

namespace OElite
{
    public interface IRestme
    {
        Uri? BaseUri { get; set; }
        string? RequestUrlPath { get; set; }

        void Add(string key, string value);
        void Add(string key, object value);
        void Add(object? value);

        void AddHeader(string header, string value, bool allowMultipleValues = false);
        void AddAuthorizationHeader(string token, string authTypePrefix = "Bearer ");


        T? HttpRequest<T>(HttpMethod method, string? keyOrRelativePath = null);
        Task<T?> HttpRequestAsync<T>(HttpMethod method, string? keyOrRelativePath = null);

        HttpResponseMessage<T?>? HttpRequestFull<T>(HttpMethod method, string? keyOrRelativePath = null,
            object? dataObject = null);

        Task<HttpResponseMessage<T?>?> HttpRequestFullAsync<T>(HttpMethod method, string? keyOrRelativePath = null,
            object? dataObject = null);

        T? Get<T>(string? keyOrRelativePath = null, object? dataObject = null) where T : class;
        Task<T?> GetAsync<T>(string? keyOrRelativePath = null, object? dataObject = null) where T : class;
        string? Get(string? keyOrRelativePath = null, object? dataObject = null);
        Task<string?> GetAsync(string? keyOrRelativePath = null, object? dataObject = null);

        T? Put<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
            where T : class;

        Task<T?> PutAsync<T>(string? keyOrRelativePath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class;

        string? Put(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null);

        Task<string?> PutAsync(string? keyOrRelativePath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null);


        T? Post<T>(string? keyOrRelativePath = null, object? dataObject = null, TimeSpan? expiryInMinutes = null)
            where T : class;

        Task<T?> PostAsync<T>(string? keyOrRelativePath = null, object? dataObject = null,
            TimeSpan? expiryInMinutes = null) where T : class;

        string? Post(string? keyOrRelativePath = null, string? dataValue = null, TimeSpan? expiryInMinutes = null);

        Task<string?> PostAsync(string? keyOrRelativePath = null, string? dataValue = null,
            TimeSpan? expiryInMinutes = null);

        T? Delete<T>(string? keyOrRelativePath = null, object? dataObject = null) where T : class;
        Task<T?> DeleteAsync<T>(string? keyOrRelativePath = null, object? dataObject = null) where T : class;
        string? Delete(string? keyOrRelativePath = null, object? dataObject = null);
        Task<string?> DeleteAsync(string? keyOrRelativePath = null, object? dataObject = null);

        #region Logging

        void LogError(string? errorMessage, Exception? ex = null, int eventId = 0);
        void LogWarning(string? errorMessage, Exception? ex = null, int eventId = 0);
        void LogInfo(string? info, Exception? ex = null, int eventId = 0);
        void LogDebug(string? debugInfo, Exception? ex = null, int eventId = 0);
        void LogFatal(string? fatalInfo, Exception? ex = null, int eventId = 0);

        #endregion


        RestMode CurrentMode { get; }

        ICacheProvider? CacheProvider { get; }
        IQueueProvider? QueueProvider { get; }
        IStorageProvider? StorageProvider { get; }
        IHttpProvider? HttpProvider { get; }
        ILogProvider? LogProvider { get; }
        IColumnarProvider? ColumnarProvider { get; }
        IStreamingProvider? StreamingProvider { get; }
        ISearchProvider? SearchProvider { get; }
    }
}