using OElite.Restme.Kafka.IntegrationTests.Models;
using OElite.Restme.Kafka;
using OElite.Restme;
using Xunit;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

public class KafkaConnectionStringIntegrationTests
{
    [Fact]
    public async Task PublishAsync_succeeds_with_bootstrap_servers_connection_string()
    {
        // Given the shared Kafka broker and the connection-string format emitted by OElite.Common
        using var rest = new Rest(new RestConfig(RestMode.Kafka)
        {
            ConnectionString = "bootstrap.servers=localhost:9092"
        });
        var topicName = $"restme-bootstrap-prefix-{Guid.NewGuid():N}";
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // When a message is published through the public Restme API
        await rest.PublishAsync(
            new UserActivityMessage
            {
                UserId = "connection-string-regression",
                Action = "publish",
                Metadata = "bootstrap.servers prefix"
            },
            topicName,
            cancellationToken: cancellationTokenSource.Token);

        // Then publication completes without a broker-resolution or delivery timeout
    }
}
