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
            value = value[BootstrapServersPrefix.Length..].Trim();
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
