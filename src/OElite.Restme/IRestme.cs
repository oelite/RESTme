using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OElite
{
    public interface IRestme
    {
        Uri BaseUri { get; set; }
        string RequestUrlPath { get; set; }

        void Add(string key, string value);
        void Add(string key, object value);
        void Add(object value);

        void AddHeader(string header, string value, bool allowMultipleValues = false);
        void AddBearerToken(string token);


        T Request<T>(HttpMethod method, string keyOrRelativePath = null);
        Task<T> RequestAsync<T>(HttpMethod method, string keyOrRelativePath = null);

        T Get<T>(string keyOrRelativePath = null);
        Task<T> GetAsync<T>(string keyOrRelativePath = null);
        string Get(string keyOrRelativePath = null);
        Task<string> GetAsync(string keyOrRelativePath = null);

        T Post<T>(string keyOrRelativePath = null, T dataObject = default(T));
        Task<T> PostAsync<T>(string keyOrRelativePath = null, T dataObject = default(T));
        string Post(string keyOrRelativePath = null, string dataValue = null);
        Task<string> PostAsync(string keyOrRelativePath = null, string dataValue = null);

        T Delete<T>(string keyOrRelativePath = null);
        Task<T> DeleteAsync<T>(string keyOrRelativePath = null);
        bool Delete(string keyOrRelativePath = null);
        Task<bool> DeleteAsync(string keyOrRelativePAth = null);
    }
}
