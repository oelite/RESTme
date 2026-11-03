using System;
using System.Linq;

namespace OElite.Restme.Kafka;

internal static class KafkaConnectionString
{
    private const string BootstrapServersPrefix = "bootstrap.servers=";
    private const string KafkaScheme = "kafka://";

    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Kafka connection string must contain broker endpoints.",
                nameof(connectionString));
        }

        var value = connectionString.Trim();

        if (value.StartsWith(BootstrapServersPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = value[BootstrapServersPrefix.Length..];
            var propertySeparator = endpoint.IndexOf('&');
            if (propertySeparator >= 0)
            {
                endpoint = endpoint[..propertySeparator];
            }

            value = endpoint.Trim();
        }

        if (value.StartsWith(KafkaScheme, StringComparison.OrdinalIgnoreCase))
        {
            value = value[KafkaScheme.Length..].Trim();
        }

        // Strip any userinfo (user:password@) carried inside inline-credential URIs
        // so that credentials can never reach BootstrapServers and, consequently,
        // librdkafka native log output on connection failures.
        value = string.Join(
            ",",
            value.Split(',').Select(endpoint =>
            {
                var trimmed = endpoint.Trim();
                var delimiter = trimmed.LastIndexOf('@');
                return delimiter > 0 ? trimmed[(delimiter + 1)..].Trim() : trimmed;
            }));

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Kafka connection string must contain broker endpoints.",
                nameof(connectionString));
        }

        return value;
    }
}
