using System.Net.Http;
using System.Threading.Tasks;
using OElite.Restme.Utils;

namespace OElite
{
    public partial class Rest
    {
        private void PrepareHttpRestme()
        {
            Initialized = true;
        }

        public HttpResponseMessage<T> HttpRequestFull<T>(HttpMethod method, string keyOrRelativePath = null,
            object dataObject = null)
        {
            _objAsParam = dataObject;
            return RestmeHttpExtensions.HttpRequestFull<T>(this, method, keyOrRelativePath);
        }

        public Task<HttpResponseMessage<T>> HttpRequestFullAsync<T>(HttpMethod method, string keyOrRelativePath = null,
            object dataObject = null)
        {
            _objAsParam = dataObject;
            return RestmeHttpExtensions.HttpRequestFullAsync<T>(this, method, keyOrRelativePath);
        }
    }
}