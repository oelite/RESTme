using System;
using System.Net;
using System.Net.Http.Headers;

namespace OElite.Restme.Utils
{
    public class HttpResponseMessage<T>
    {
        public T Data { get; set; }

        public HttpHeaders Headers { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public DateTime ReceivedOnUtc { get; set; }

        public Exception ErrorMessage { get; set; }
    }
}