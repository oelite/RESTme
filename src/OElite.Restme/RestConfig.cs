using System.Text;
using Newtonsoft.Json;

namespace OElite
{
    public class RestConfig
    {
        public RestMode OperationMode { get; set; }
        public Encoding DefaultEncoding { get; set; }
        public JsonSerializerSettings SerializerSettings { get; set; }
        public bool UseRestConvertForCollectionSerialization { get; set; }

        public RestConfig(JsonSerializerSettings jsonSerializerSettings = null, Encoding encoding = null,
            bool useRestConvertForCollectionSerialization = true)
        {
            this.SerializerSettings = jsonSerializerSettings ??
                                      new JsonSerializerSettings() {ContractResolver = new OEliteJsonResolver()};
            this.DefaultEncoding = encoding ?? Encoding.UTF8;
            this.UseRestConvertForCollectionSerialization = useRestConvertForCollectionSerialization;
        }
    }
}