using FluentAssertions;
using OElite.Restme.Kafka;
using Xunit;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

public class KafkaConnectionStringTests
{
    [Theory]
    [InlineData("bootstrap.servers=localhost:9092", "localhost:9092")]
    [InlineData("kafka://localhost:9092", "localhost:9092")]
    [InlineData("localhost:9092", "localhost:9092")]
    [InlineData("  bootstrap.servers=host1:9092,host2:9092  ", "host1:9092,host2:9092")]
    [InlineData("bootstrap.servers=localhost:9092&sasl.mechanism=PLAIN&security.protocol=SaslSsl&sasl.username=test-user&sasl.password=secret-value", "localhost:9092")]
    public void Normalize_returns_broker_endpoints_for_supported_connection_strings(
        string connectionString,
        string expectedBootstrapServers)
    {
        // Given a supported Kafka connection-string form
        // When the broker endpoints are normalized
        var bootstrapServers = KafkaConnectionString.Normalize(connectionString);

        // Then the client receives only the broker endpoint value
        bootstrapServers.Should().Be(expectedBootstrapServers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bootstrap.servers=")]
    [InlineData("bootstrap.servers=&sasl.password=secret-value")]
    [InlineData("kafka://")]
    public void Normalize_rejects_connection_strings_without_broker_endpoints(string connectionString)
    {
        // Given a connection string without a broker endpoint
        // When normalization is attempted
        var action = () => KafkaConnectionString.Normalize(connectionString);

        // Then it fails with an actionable argument error without echoing input
        action.Should().Throw<ArgumentException>()
            .WithMessage("Kafka connection string must contain broker endpoints.*");
    }
}
