using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace OElite.Restme
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


        T Request<T>(HttpMethod method, string relativeUrlPath = null);
        Task<T> RequestAsync<T>(HttpMethod method, string relativePath = null);

        T Get<T>(string relativeUrlPath = null);
        Task<T> GetAsync<T>(string relativeUrlPath = null);

        T Post<T>(string relativeUrlPath = null);
        Task<T> PostAsync<T>(string relativeUrlPath = null);

    }
}
