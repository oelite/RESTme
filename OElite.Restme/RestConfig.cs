using System;
using System.Text;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace OElite.Restme
{
    public class RestConfig
    {
        public RestMode OperationMode { get; set; }
        public Encoding DefaultEncoding { get; set; }

        // Primary authentication fields (recommended for all providers)
        public string? AuthKey { get; set; }
        public string? AuthSecret { get; set; }

        // Provider-specific configuration
        public string? ConnectionString { get; set; }
        public string? Endpoint { get; set; }
        public string? Region { get; set; }

        /// <summary>
        /// InstanceName can be BucketName for S3, Instance name for database, vhost for RabbitMQ etc.
        /// </summary>
        public string? InstanceName { get; set; }

        public string? RootPath { get; set; }

        // Additional settings
        public bool RestSsl { get; set; }
        public JsonSerializerSettings? SerializerSettings { get; set; }
        public bool UseRestConvertForCollectionSerialization { get; set; }
        public int DefaultTimeout { get; set; }

        public RestConfig(RestMode restMode, JsonSerializerSettings? jsonSerializerSettings = null,
            Encoding? encoding = null,
            bool useRestConvertForCollectionSerialization = true, int timeout = 0)
        {
            OperationMode = restMode;
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

        /// <summary>
        /// Virtual method that providers can override for custom connection string parsing.
        /// Default implementation handles common URI-based protocols.
        /// </summary>
        public virtual void ParseConnectionString()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;

            // Default implementation: try URI-based parsing for common patterns
            ParseUriBasedConnectionString();
        }

        /// <summary>
        /// Common URI-based parsing for protocols like redis://, amqp://, etc.
        /// </summary>
        protected void ParseUriBasedConnectionString()
        {
            try
            {
                Uri uri;

                // Handle protocol-specific URIs
                if (ConnectionString.StartsWith("redis://"))
                {
                    uri = new Uri(ConnectionString);
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var creds = uri.UserInfo.Split(':');
                        AuthSecret = creds.Length > 1 ? creds[1] : creds[0];
                    }

                    Endpoint = $"{uri.Host}:{uri.Port}";
                    InstanceName = uri.AbsolutePath.TrimStart('/');
                }
                else if (ConnectionString.StartsWith("amqp://"))
                {
                    uri = new Uri(ConnectionString);
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var creds = uri.UserInfo.Split(':');
                        AuthKey = creds[0]; // username
                        AuthSecret = creds[1]; // password
                    }

                    Endpoint = $"{uri.Host}:{uri.Port}";
                    InstanceName = uri.AbsolutePath.TrimStart('/'); // vhost
                }
                else if (ConnectionString.StartsWith("clickhouse://"))
                {
                    var cleanUri = ConnectionString.Replace("clickhouse://", "http://");
                    uri = new Uri(cleanUri);
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var creds = uri.UserInfo.Split(':');
                        AuthKey = creds[0]; // username
                        AuthSecret = creds[1]; // password
                    }

                    Endpoint = $"{uri.Host}:{uri.Port}";
                    InstanceName = uri.AbsolutePath.TrimStart('/'); // database
                }
                else if (ConnectionString.StartsWith("kafka://"))
                {
                    var cleanUri = ConnectionString.Replace("kafka://", "http://");
                    uri = new Uri(cleanUri);
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var creds = uri.UserInfo.Split(':');
                        AuthKey = creds[0]; // SASL username
                        AuthSecret = creds[1]; // SASL password
                    }

                    Endpoint = $"{uri.Host}:{uri.Port}";
                }
                else if (ConnectionString.StartsWith("opensearch://"))
                {
                    var cleanUri = ConnectionString.Replace("opensearch://", "http://");
                    uri = new Uri(cleanUri);
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        var creds = uri.UserInfo.Split(':');
                        AuthKey = creds[0]; // username
                        AuthSecret = creds[1]; // password
                    }

                    Endpoint = $"{uri.Host}:{uri.Port}";
                }
                // Add more common URI patterns as needed
            }
            catch (Exception)
            {
                // If URI parsing fails, leave fields as-is
                // Individual providers can handle custom formats
            }
        }
    }
}