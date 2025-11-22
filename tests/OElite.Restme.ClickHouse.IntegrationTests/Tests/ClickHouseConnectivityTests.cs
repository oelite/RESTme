using Xunit;
using Xunit.Abstractions;
using OElite.Restme.ClickHouse.IntegrationTests.Infrastructure;

namespace OElite.Restme.ClickHouse.IntegrationTests.Tests;

/// <summary>
/// Basic connectivity tests for ClickHouse container
/// </summary>
public class ClickHouseConnectivityTests : ClickHouseTestBase
{
    private readonly ITestOutputHelper _output;

    public ClickHouseConnectivityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Container_ShouldRespondToHttpPing()
    {
        // Arrange & Act
        var isConnected = await TestContainerConnectivityAsync();

        // Assert
        Assert.True(isConnected, "ClickHouse container should respond to HTTP ping requests");
        _output.WriteLine("✅ ClickHouse container HTTP connectivity verified");
    }

    [Fact]
    public async Task Container_ShouldAcceptBasicQuery()
    {
        // Arrange
        var mappedPort = _clickHouseContainer.GetMappedPublicPort(8123);
        using var httpClient = new HttpClient();

        // Add basic authentication for our test user
        var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test_user:test_password"));
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

        // Act
        var response = await httpClient.PostAsync(
            $"http://localhost:{mappedPort}/",
            new StringContent("SELECT 1"));

        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Basic query should succeed. Status: {response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Query response: {content}");
        _output.WriteLine("✅ ClickHouse container accepts basic queries");
    }
}