using System.Text;
using Newtonsoft.Json;

namespace OElite
{
    public class RestConfig
    {
        public RestMode OperationMode { get; set; }
        public Encoding DefaultEncoding { get; }

        public string RestKey { get; }
        public string RestSecret { get; }
        public bool RestSsl { get; }
        public JsonSerializerSettings SerializerSettings { get; }
        public bool UseRestConvertForCollectionSerialization { get; }
        public int DefaultTimeout { get; }

        public RestConfig(string restKey, string restSecret, bool restSsl,
            JsonSerializerSettings? jsonSerializerSettings = null,
            Encoding? encoding = null,
            bool useRestConvertForCollectionSerialization = true, int timeout = 0)
        {
            SerializerSettings = jsonSerializerSettings ??
                                 new JsonSerializerSettings
                                 {
                                     ContractResolver = new OEliteJsonResolver(),
                                     NullValueHandling = NullValueHandling.Ignore,
                                     MissingMemberHandling = MissingMemberHandling.Ignore
                                 };
            DefaultEncoding = encoding ?? Encoding.UTF8;
            RestKey = restKey;
            RestSecret = restSecret;
            RestSsl = restSsl;
            UseRestConvertForCollectionSerialization = useRestConvertForCollectionSerialization;
            DefaultTimeout = timeout > 0 ? timeout : 0;
        }
    }
}