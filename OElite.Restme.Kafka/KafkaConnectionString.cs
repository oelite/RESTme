using System;

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

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Kafka connection string must contain broker endpoints.",
                nameof(connectionString));
        }

        return value;
    }
}
