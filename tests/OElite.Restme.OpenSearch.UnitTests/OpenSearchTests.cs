using OElite;
using OElite.Abstractions;
using OElite.Restme.OpenSearch;
using Xunit;

namespace OElite.Restme.OpenSearch.UnitTests
{
    public class OpenSearchProviderTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var connectionString = "opensearch://localhost:9200";
            var config = new RestConfig { OperationMode = RestMode.OpenSearch };

            // Act
            var provider = new OpenSearchProvider(connectionString, config);

            // Assert
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<ISearchProvider>(provider);
        }

        [Fact]
        public async Task IndexAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new OpenSearchProvider("opensearch://localhost:9200",
                new RestConfig { OperationMode = RestMode.OpenSearch });
            var document = new TestDocument { Id = "123", Name = "test" };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.IndexAsync(document, "test-index"));
            Assert.Contains("OpenSearch.Client", exception.Message);
        }

        [Fact]
        public async Task SearchAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new OpenSearchProvider("opensearch://localhost:9200",
                new RestConfig { OperationMode = RestMode.OpenSearch });
            var query = new SearchQuery { Query = "test:*" };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SearchAsync<TestDocument>(query, "test-index"));
            Assert.Contains("OpenSearch.Client", exception.Message);
        }

        private class TestDocument
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }

    public class OpenSearchServiceFactoryTests
    {
        [Fact]
        public void CreateSearchProvider_ReturnsOpenSearchProvider()
        {
            // Arrange
            var factory = new OpenSearchServiceFactory();

            // Act
            var provider = factory.CreateSearchProvider("opensearch://localhost:9200",
                new RestConfig { OperationMode = RestMode.OpenSearch });

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<OpenSearchProvider>(provider);
        }

        [Fact]
        public void CreateUnsupportedProvider_ThrowsNotImplementedException()
        {
            // Arrange
            var factory = new OpenSearchServiceFactory();

            // Act & Assert
            Assert.Throws<NotImplementedException>(
                () => factory.CreateCacheProvider("", new RestConfig()));
        }

        [Fact]
        public async Task SetIndexTTLAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new OpenSearchProvider("opensearch://localhost:9200",
                new RestConfig { OperationMode = RestMode.OpenSearch });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SetIndexTTLAsync("test-index", "timestamp", TimeSpan.FromDays(30)));
            Assert.Contains("OpenSearch.Client", exception.Message);
        }

        [Fact]
        public async Task SetIndexLifecyclePolicyAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new OpenSearchProvider("opensearch://localhost:9200",
                new RestConfig { OperationMode = RestMode.OpenSearch });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SetIndexLifecyclePolicyAsync("test-index", TimeSpan.FromDays(90)));
            Assert.Contains("OpenSearch.Client", exception.Message);
        }
    }
}