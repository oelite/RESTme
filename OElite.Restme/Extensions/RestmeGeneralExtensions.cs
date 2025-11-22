using System.Diagnostics;

namespace OElite.Restme
{
    public static class RestmeGeneralExtensions
    {
        /// <summary>
        /// Use base uri to identify the use of current Restme client
        /// </summary>
        /// <param name="restme"></param>
        public static void PrepareRestMode(this Rest restme)
        {
            var connectionString = restme.Configuration.ConnectionString;
            if (!connectionString.IsNotNullOrEmpty()) return;

            Debug.Assert(connectionString != null, "restme.ConnectionString != null");
            if ((connectionString.Contains("defaultendpointsprotocol") &&
                 connectionString.Contains("accountname") &&
                 connectionString.Contains("accountkey")) ||
                (connectionString.Contains("usedevelopmentstorage") &&
                 connectionString.Contains("true")
                ))
            {
                restme.Configuration.OperationMode = RestMode.Azure;
            }
            else if (connectionString.ToLower().Contains("redis.cache.windows.net") ||
                     connectionString.ToLower().Contains(":6379") ||
                     connectionString.ToLower().Contains(":6380"))
            {
                restme.Configuration.OperationMode = RestMode.Redis;
            }
            else if (connectionString.IsS3Provider())
            {
                restme.Configuration.OperationMode = RestMode.S3;
            }
            else if (connectionString.IsClickHouseProvider())
            {
                restme.Configuration.OperationMode = RestMode.ClickHouse;
            }
            else if (connectionString.IsKafkaProvider())
            {
                restme.Configuration.OperationMode = RestMode.Kafka;
            }
            else if (connectionString.IsOpenSearchProvider())
            {
                restme.Configuration.OperationMode = RestMode.OpenSearch;
            }
            else if (connectionString.IsRabbitMQProvider())
            {
                restme.Configuration.OperationMode = RestMode.RabbitMq;
            }

            restme.InitializeProviders();
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// Check if connection string is for S3 provider
        /// </summary>
        public static bool IsS3Provider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;

            var lower = connectionString.ToLower();
            return lower.Contains("amazonaws.com") ||
                   lower.Contains("s3://") ||
                   lower.Contains("accesskeyid") && lower.Contains("secretaccesskey");
        }

        /// <summary>
        /// Check if connection string is for ClickHouse provider
        /// </summary>
        public static bool IsClickHouseProvider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;

            var lower = connectionString.ToLower();
            return lower.StartsWith("clickhouse://") ||
                   lower.Contains(":8123") || // ClickHouse default HTTP port
                   lower.Contains(":9000") || // ClickHouse default native port
                   lower.Contains("clickhouse");
        }

        /// <summary>
        /// Check if connection string is for Kafka provider
        /// </summary>
        public static bool IsKafkaProvider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;

            var lower = connectionString.ToLower();
            return lower.StartsWith("kafka://") ||
                   lower.Contains(":9092") || // Kafka default port
                   lower.Contains(":9093") || // Kafka SSL port
                   lower.Contains("bootstrap.servers") ||
                   lower.Contains("kafka");
        }

        /// <summary>
        /// Check if connection string is for OpenSearch provider
        /// </summary>
        public static bool IsOpenSearchProvider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;

            var lower = connectionString.ToLower();
            return lower.StartsWith("opensearch://") ||
                   lower.Contains(":9200") || // OpenSearch default port
                   lower.Contains("opensearch") ||
                   lower.Contains("elasticsearch"); // OpenSearch is ES compatible
        }

        /// <summary>
        /// Check if connection string is for RabbitMQ provider
        /// </summary>
        public static bool IsRabbitMQProvider(this string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return false;

            var lower = connectionString.ToLower();
            return lower.StartsWith("amqp://") ||
                   lower.StartsWith("amqps://") ||
                   lower.Contains(":5672") || // RabbitMQ default port
                   lower.Contains(":5671") || // RabbitMQ SSL port
                   lower.Contains("rabbitmq");
        }
    }
}