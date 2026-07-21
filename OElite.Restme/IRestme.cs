using System;
using System.Net.Http;
using System.Threading.Tasks;
using OElite.Restme.Abstractions;
using OElite.Restme.Utils;

namespace OElite.Restme
{
    public interface IRestme : IDisposable
    {
        RestConfig Configuration { get; }
        string? RequestUrlPath { get; set; }

        void Add(string key, string value);
        void Add(string key, object value);
        void Add(object? value);

        void AddHeader(string header, string value, bool allowMultipleValues = false);
        void AddAuthorizationHeader(string token, string authTypePrefix = "Bearer ");



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

        /// <summary>
        /// Get a provider instance based on the current RestMode configuration.
        /// </summary>
        /// <typeparam name="T">The provider interface type to retrieve.</typeparam>
        /// <param name="name">Optional provider name for managing multiple instances of the same type. Defaults to "default".</param>
        /// <returns>A provider instance if available for the current mode, null otherwise.</returns>
        T? GetProvider<T>(string name = "default") where T : class, IRestmeProvider;
    }
}