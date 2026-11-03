using Confluent.Kafka;
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

    [Fact]
    public async Task Inline_uri_credentials_never_reach_stderr_when_connection_fails()
    {
        // Regression for MR !19 review: librdkafka echoes bootstrap.servers verbatim in
        // native error output on connection failure, so inline userinfo must be stripped
        // before the client is built. librdkafka logs and errors are routed through the
        // redacting handler onto Console.Error, which this test captures.
        var password = $"kafka-uri-secret-{Guid.NewGuid():N}";
        var config = new RestConfig(RestMode.Kafka)
        {
            ConnectionString = $"kafka://test-user:{password}@127.0.0.1:1",
            AuthKey = "test-user",
            AuthSecret = password
        };

        using var errorCapture = new StringWriter();
        var originalError = Console.Error;
        try
        {
            Console.SetError(errorCapture);

            using var connection = new KafkaConnection(config.ConnectionString, config);
            var captured = errorCapture.ToString();

            // Repeatedly attempt a delivery against the refused endpoint so librdkafka
            // emits and flushes connection-failure log output, then check for it.
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline &&
                   !captured.Contains("brokers are down", StringComparison.OrdinalIgnoreCase))
            {
                using var attempt = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                try
                {
                    await connection.PublishAsync(
                        new { value = "ping" },
                        $"stderr-regression-{Guid.NewGuid():N}",
                        "stderr-regression",
                        attempt.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected: the endpoint refuses connections.
                }
                catch (ProduceException<string, string>)
                {
                    // Expected: delivery failure is what triggers native error output.
                }

                await Task.Delay(250);
                captured = errorCapture.ToString();
            }
        }
        finally
        {
            Console.SetError(originalError);
        }

        var capturedError = errorCapture.ToString();

        // The failure output must actually be observable on the captured channel (non-vacuous),
        // and it must never carry credential material from the inline URI.
        capturedError.Should().Contain("brokers are down");
        capturedError.Should().NotContain(password);
        capturedError.Should().NotContain("test-user");
    }
}

[CollectionDefinition("Console output", DisableParallelization = true)]
public class ConsoleOutputCollection;
