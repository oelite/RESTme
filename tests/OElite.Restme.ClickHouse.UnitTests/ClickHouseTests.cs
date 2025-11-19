using System;
using System.Linq.Expressions;
using OElite;
using OElite.Abstractions;
using OElite.Restme.ClickHouse;
using Xunit;

namespace OElite.Restme.ClickHouse.UnitTests
{
    public class ClickHouseExpressionTranslatorTests
    {
        [Fact]
        public void Translate_SimpleEquality_ReturnsCorrectSql()
        {
            // Arrange
            var translator = new ClickHouseExpressionTranslator();
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 123;

            // Act
            var sql = translator.Translate(expression);
            var parameters = translator.GetParameters();

            // Assert
            Assert.Equal("(id = @p0)", sql);
            Assert.Single(parameters);
            Assert.Equal(123, parameters["@p0"]);
        }

        [Fact]
        public void Translate_StringContains_ReturnsCorrectSql()
        {
            // Arrange
            var translator = new ClickHouseExpressionTranslator();
            Expression<Func<TestEntity, bool>> expression = e => e.Name.Contains("test");

            // Act
            var sql = translator.Translate(expression);
            var parameters = translator.GetParameters();

            // Assert
            Assert.Equal("(name LIKE @p0)", sql);
            Assert.Single(parameters);
            Assert.Equal("%test%", parameters["@p0"]);
        }

        [Fact]
        public void Translate_ComplexExpression_ReturnsCorrectSql()
        {
            // Arrange
            var translator = new ClickHouseExpressionTranslator();
            Expression<Func<TestEntity, bool>> expression = e =>
                e.Id > 100 && e.Name.Contains("test") && e.Status == "active";

            // Act
            var sql = translator.Translate(expression);
            var parameters = translator.GetParameters();

            // Assert
            Assert.Equal("(((id > @p0) AND (name LIKE @p1)) AND (status = @p2))", sql);
            Assert.Equal(3, parameters.Count);
            Assert.Equal(100, parameters["@p0"]);
            Assert.Equal("%test%", parameters["@p1"]);
            Assert.Equal("active", parameters["@p2"]);
        }

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
        }
    }

    public class ClickHouseSqlBuilderTests
    {
        [Fact]
        public void BuildInsertSql_SingleEntity_ReturnsCorrectSql()
        {
            // Arrange
            var entity = new TestEntity { Id = 123, Name = "Test", Status = "active" };

            // Act
            var sql = ClickHouseSqlBuilder.BuildInsertSql(entity, "test_table");

            // Assert
            Assert.Equal("INSERT INTO test_table (id, name, status) VALUES (@p0, @p1, @p2)", sql);
        }

        [Fact]
        public void BuildBulkInsertSql_ReturnsCorrectSql()
        {
            // Act
            var sql = ClickHouseSqlBuilder.BuildBulkInsertSql<TestEntity>("test_table");

            // Assert
            Assert.Equal("INSERT INTO test_table (id, name, status) VALUES ", sql);
        }

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
        }
    }

    public class ClickHouseTableBuilderTests
    {
        [Fact]
        public void BuildCreateTableSql_MergeTree_ReturnsCorrectSql()
        {
            // Act
            var sql = ClickHouseTableBuilder.BuildCreateTableSql<TestEntity>(
                "test_table", ClickHouseEngine.MergeTree);

            // Assert
            Assert.Contains("CREATE TABLE IF NOT EXISTS test_table", sql);
            Assert.Contains("ENGINE = MergeTree()", sql);
            Assert.Contains("ORDER BY tuple()", sql);
        }

        [Fact]
        public void BuildCreateTableSql_ReplacingMergeTree_ReturnsCorrectSql()
        {
            // Act
            var sql = ClickHouseTableBuilder.BuildCreateTableSql<TestEntity>(
                "test_table", ClickHouseEngine.ReplacingMergeTree);

            // Assert
            Assert.Contains("ENGINE = ReplacingMergeTree()", sql);
        }

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }

    public class ClickHouseProviderTests
    {
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange
            var connectionString = "clickhouse://localhost:8123";
            var config = new RestConfig { OperationMode = RestMode.ClickHouse };

            // Act
            var provider = new ClickHouseProvider(connectionString, config);

            // Assert
            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IColumnarProvider>(provider);
        }

        [Fact]
        public async Task InsertAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new ClickHouseProvider("clickhouse://localhost:8123",
                new RestConfig { OperationMode = RestMode.ClickHouse });
            var entity = new TestEntity { Id = 123, Name = "Test" };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.InsertAsync(entity));
            Assert.Contains("ClickHouse.Client", exception.Message);
        }

        [Fact]
        public async Task QueryAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new ClickHouseProvider("clickhouse://localhost:8123",
                new RestConfig { OperationMode = RestMode.ClickHouse });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.QueryAsync<TestEntity>("SELECT * FROM test_table"));
            Assert.Contains("ClickHouse.Client", exception.Message);
        }

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }

    public class ClickHouseServiceFactoryTests
    {
        [Fact]
        public void CreateColumnarProvider_ReturnsClickHouseProvider()
        {
            // Arrange
            var factory = new ClickHouseServiceFactory();

            // Act
            var provider = factory.CreateColumnarProvider("clickhouse://localhost:8123",
                new RestConfig { OperationMode = RestMode.ClickHouse });

            // Assert
            Assert.NotNull(provider);
            Assert.IsType<ClickHouseProvider>(provider);
        }

        [Fact]
        public void CreateUnsupportedProvider_ThrowsNotImplementedException()
        {
            // Arrange
            var factory = new ClickHouseServiceFactory();

            // Act & Assert
            Assert.Throws<NotImplementedException>(
                () => factory.CreateCacheProvider("", new RestConfig()));
        }

        [Fact]
        public async Task SetTableTTLAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new ClickHouseProvider("clickhouse://localhost:8123",
                new RestConfig { OperationMode = RestMode.ClickHouse });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.SetTableTTLAsync("test_table", "created_at + INTERVAL 30 DAY"));
            Assert.Contains("ClickHouse.Client", exception.Message);
        }

        [Fact]
        public async Task CreateTTLIndexAsync_WithoutClientLibrary_ThrowsNotImplementedException()
        {
            // Arrange
            var provider = new ClickHouseProvider("clickhouse://localhost:8123",
                new RestConfig { OperationMode = RestMode.ClickHouse });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotImplementedException>(
                () => provider.CreateTTLIndexAsync("test_table", "created_at", TimeSpan.FromDays(30)));
            Assert.Contains("ClickHouse.Client", exception.Message);
        }
    }
}