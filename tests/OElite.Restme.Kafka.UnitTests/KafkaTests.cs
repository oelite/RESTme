using OElite;
using OElite.Abstractions;
using OElite.Restme.Kafka;
using Xunit;

namespace OElite.Restme.Kafka.UnitTests
{
    public class KafkaProviderTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var connectionString = "kafka://localhost:9092";
            var config = new RestConfig { OperationMode = RestMode.Kafka };

            // Act
            var provider = new KafkaProvider(connectionString, config);

            // Assert
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IStreamingProvider>(provider);
        }

        [Fact]
        public async Task PublishAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new KafkaProvider("kafka://localhost:9092",
                new RestConfig { OperationMode = RestMode.Kafka });
            var message = new TestMessage { Id = "123", Content = "test" };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.PublishAsync(message, "test-topic"));
            Assert.Contains("Confluent.Kafka", exception.Message);
        }

        [Fact]
        public async Task SubscribeAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new KafkaProvider("kafka://localhost:9092",
                new RestConfig { OperationMode = RestMode.Kafka });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SubscribeAsync<TestMessage>("test-topic", "test-group",
                    async (msg) => { }));
            Assert.Contains("Confluent.Kafka", exception.Message);
        }

        private class TestMessage
        {
            public string Id { get; set; }
            public string Content { get; set; }
        }
    }

    public class KafkaServiceFactoryTests
    {
        [Fact]
        public void CreateStreamingProvider_ReturnsKafkaProvider()
        {
            // Arrange
            var factory = new KafkaServiceFactory();

            // Act
            var provider = factory.CreateStreamingProvider("kafka://localhost:9092",
                new RestConfig { OperationMode = RestMode.Kafka });

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<KafkaProvider>(provider);
        }

        [Fact]
        public void CreateUnsupportedProvider_ThrowsNotImplementedException()
        {
            // Arrange
            var factory = new KafkaServiceFactory();

            // Act & Assert
            Assert.Throws<NotImplementedException>(
                () => factory.CreateCacheProvider("", new RestConfig()));
        }

        [Fact]
        public async Task SetTopicRetentionAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new KafkaProvider("kafka://localhost:9092",
                new RestConfig { OperationMode = RestMode.Kafka });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SetTopicRetentionAsync("test-topic", TimeSpan.FromDays(7)));
            Assert.Contains("Confluent.Kafka", exception.Message);
        }

        [Fact]
        public async Task SetMessageExpiryAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new KafkaProvider("kafka://localhost:9092",
                new RestConfig { OperationMode = RestMode.Kafka });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SetMessageExpiryAsync("test-topic", TimeSpan.FromHours(24)));
            Assert.Contains("Confluent.Kafka", exception.Message);
        }
    }
}