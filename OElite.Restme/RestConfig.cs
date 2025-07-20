using System.Text;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace OElite
{
    public class RestConfig
    {
        public RestMode OperationMode { get; set; }
        public Encoding DefaultEncoding { get; set; }

        public string? RestKey { get; set; }
        public string? RestSecret { get; set; }
        public bool RestSsl { get; set; }
        public JsonSerializerSettings? SerializerSettings { get; set; }
        public bool UseRestConvertForCollectionSerialization { get; set; }
        public int DefaultTimeout { get; set; }

        public RestConfig(JsonSerializerSettings? jsonSerializerSettings = null, Encoding? encoding = null,
            bool useRestConvertForCollectionSerialization = true, int timeout = 0)
        {
            SerializerSettings = jsonSerializerSettings ??
                                 new JsonSerializerSettings()
                                 {
                                     ContractResolver = new OEliteJsonResolver(),
                                     NullValueHandling = NullValueHandling.Ignore,
                                     MissingMemberHandling = MissingMemberHandling.Ignore
                                 };
            DefaultEncoding = encoding ?? Encoding.UTF8;
            UseRestConvertForCollectionSerialization = useRestConvertForCollectionSerialization;
            DefaultTimeout = timeout > 0 ? timeout : 0;
        }
    }
}