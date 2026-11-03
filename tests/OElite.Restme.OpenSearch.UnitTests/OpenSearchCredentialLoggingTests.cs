using OElite.Restme.Abstractions;
using OElite.Restme.OpenSearch;
using Xunit;

namespace OElite.Restme.OpenSearch.UnitTests;

[CollectionDefinition("OpenSearch console capture", DisableParallelization = true)]
public class OpenSearchConsoleCaptureCollection;

[Collection("OpenSearch console capture")]
public class OpenSearchCredentialLoggingTests
{
    [Theory]
    [InlineData("opensearch://user-marker:secret-marker@localhost:9200")]
    [InlineData("https://user-marker:secret-marker@localhost:9200")]
    [InlineData("user-marker:secret-marker@localhost:9200")]
    public void Constructor_DoesNotWriteCredentialsToConsole(string connectionString)
    {
        var config = new RestConfig(RestMode.OpenSearch) { ConnectionString = connectionString };
        var output = CaptureConsole(() =>
        {
            using var provider = new OpenSearchProvider(config);
        });

        Assert.DoesNotContain("user-marker", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-marker", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_InvalidConnectionString_DoesNotWriteCredentialsToConsole()
    {
        var config = new RestConfig(RestMode.OpenSearch)
        {
            ConnectionString = "opensearch://user-marker:secret-marker@localhost:badport"
        };
        var output = CaptureConsole(() => Assert.Throws<UriFormatException>(() => new OpenSearchProvider(config)));

        Assert.DoesNotContain("user-marker", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-marker", output, StringComparison.OrdinalIgnoreCase);
    }

    private static string CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return output.ToString() + error;
    }
}
