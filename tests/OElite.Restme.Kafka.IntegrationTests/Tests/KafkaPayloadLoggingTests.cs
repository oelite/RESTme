using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using OElite.Restme.Kafka.IntegrationTests.Models;
using Xunit;

namespace OElite.Restme.Kafka.IntegrationTests.Tests;

/// <summary>
/// Regression tests for BUG-19: Kafka message payloads and user identifiers must never
/// appear in console/log output across publish, consume, and deserialization-failure paths.
/// Uses the shared oelite-stack Kafka broker (canonical localhost:9092) — no mocks.
/// </summary>
[Collection("Console output")]
public class KafkaPayloadLoggingTests
{
    private static KafkaConnection CreateConnection()
    {
        var bootstrap = Environment.GetEnvironmentVariable("OELITE_KAFKA_BOOTSTRAP") ?? "localhost:9092";
        return new KafkaConnection($"kafka://{bootstrap}");
    }

    private static string Sentinel() => $"pii-sentinel-{Guid.NewGuid():N}";

    [Fact]
    public async Task PublishBatchAsync_never_logs_payload_content()
    {
        var sentinel = Sentinel();
        var topic = $"bug19-pub-{Guid.NewGuid():N}";
        var messages = new List<UserActivityMessage>
        {
            new() { UserId = sentinel, Action = "login", Timestamp = DateTime.UtcNow, Metadata = sentinel },
            new() { UserId = "user-b", Action = "logout", Timestamp = DateTime.UtcNow, Metadata = "ordinary" }
        };

        using var output = new StringWriter();
        var original = Console.Out;
        Exception? failure = null;
        try
        {
            Console.SetOut(output);
            using var connection = CreateConnection();
            try
            {
                await connection.PublishBatchAsync(messages, topic, m => m.UserId ?? "key");
            }
            catch (Exception ex)
            {
                // Delivery may fail in constrained environments; the log statements
                // under test execute BEFORE delivery, so the capture is still valid.
                failure = ex;
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        var captured = output.ToString();

        // Sanity: the publish path actually executed (topic metadata is expected in logs).
        captured.Should().Contain(topic);

        // Payload content and user identifiers must never appear.
        captured.Should().NotContain(sentinel);
        captured.Should().NotContain(messages[0].Action);
        captured.Should().NotContain("Publishing individual message: {");
    }

    [Fact]
    public async Task SubscribeAsync_never_logs_consumed_payload_or_user_identifiers()
    {
        var sentinelUserId = Sentinel();
        var sentinelPayload = Sentinel();
        var topic = $"bug19-sub-{Guid.NewGuid():N}";
        var group = $"bug19-group-{Guid.NewGuid():N}";

        using var connection = CreateConnection();

        // Publish first so the consumer picks messages up quickly.
        await connection.PublishAsync(
            new UserActivityMessage
            {
                UserId = sentinelUserId,
                Action = sentinelPayload,
                Timestamp = DateTime.UtcNow,
                Metadata = sentinelPayload
            },
            topic,
            "bug19-key");

        using var output = new StringWriter();
        var original = Console.Out;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            Console.SetOut(output);
            await connection.SubscribeAsync<UserActivityMessage>(
                topic,
                group,
                _ =>
                {
                    cts.Cancel();
                    return Task.CompletedTask;
                },
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the handler cancels after receiving a message.
        }
        finally
        {
            Console.SetOut(original);
        }

        var captured = output.ToString();

        // Sanity: the consume loop must have run and actually started.
        captured.Should().Contain("message loop");

        // Consumed payload content and user identifiers must never appear.
        captured.Should().NotContain(sentinelPayload);
        captured.Should().NotContain(sentinelUserId);
        captured.Should().NotContain("Consumer received message: '{");
        captured.Should().NotContain("Successfully deserialized message for user:");
    }

    [Fact]
    public async Task SubscribeAsync_deserialization_failure_never_logs_raw_payload()
    {
        var sentinel = Sentinel();
        var topic = $"bug19-bad-{Guid.NewGuid():N}";
        var group = $"bug19-bad-group-{Guid.NewGuid():N}";

        using var connection = CreateConnection();

        // Publish a JSON string value — when deserialized as UserActivityMessage it is
        // invalid JSON for that shape, exercising the JsonException path.
        await connection.PublishAsync($"{{ \"broken\": \"{sentinel}\"", topic, "bad-key");

        using var output = new StringWriter();
        var original = Console.Out;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            Console.SetOut(output);
            await connection.SubscribeAsync<UserActivityMessage>(
                topic,
                group,
                _ => Task.CompletedTask,
                cts.Token);
        }
        catch (JsonException)
        {
            // Expected: deserialization failure propagates after the (redacted) diagnostic.
        }
        catch (OperationCanceledException)
        {
            // Timeout safety: treat as failure to reach the assertion below.
        }
        finally
        {
            Console.SetOut(original);
        }

        var captured = output.ToString();

        // The malformed-payload diagnostic must fire but never include the raw payload.
        captured.Should().Contain("Failed to deserialize message");
        captured.Should().NotContain(sentinel);
        captured.Should().NotContain("\"broken\"");
    }
}
