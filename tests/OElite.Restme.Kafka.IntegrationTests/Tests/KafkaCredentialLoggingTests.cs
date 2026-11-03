using FluentAssertions;
using OElite.Restme.Abstractions;
using OElite.Restme.Kafka;
using Xunit;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

[Collection("Console output")]
public class KafkaCredentialLoggingTests
{
    [Theory]
    [InlineData("PLAIN", "SaslPlaintext")]
    [InlineData("SCRAM-SHA-256", "SaslSsl")]
    public void Provider_initialization_does_not_log_sasl_password(string mechanism, string protocol)
    {
        var password = $"kafka-secret-{Guid.NewGuid():N}";
        var config = new RestConfig(RestMode.Kafka)
        {
            ConnectionString = "kafka://localhost:9092",
            AuthKey = "test-user",
            AuthSecret = password,
            SaslMechanism = mechanism,
            SecurityProtocol = protocol
        };

        using var output = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            using var provider = new KafkaProvider(config);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().NotContain(password);
        output.ToString().Should().NotContain(config.AuthKey);
    }

    [Fact]
    public void Provider_initialization_does_not_log_inline_uri_credentials()
    {
        var password = $"kafka-uri-secret-{Guid.NewGuid():N}";
        var config = new RestConfig(RestMode.Kafka)
        {
            ConnectionString = $"kafka://test-user:{password}@localhost:9092",
            AuthKey = "test-user",
            AuthSecret = password
        };

        using var output = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(output);
            using var provider = new KafkaProvider(config);
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().Should().NotContain(password);
        output.ToString().Should().NotContain("test-user");
    }
}

[CollectionDefinition("Console output", DisableParallelization = true)]
public class ConsoleOutputCollection;
