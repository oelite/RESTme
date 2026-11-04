using OElite.Restme.ClickHouse;
using Xunit;

namespace OElite.Restme.ClickHouse.UnitTests;

// Regression tests for GitLab issue #7:
// - 'User=' (any case) must be accepted as the username key in ClickHouse connection strings
// - No credential material may be written to console/log output by the connection wrapper
public class ClickHouseConnectionStringTests
{
    [Theory]
    [InlineData("User", "OELITE")]
    [InlineData("user", "oelite")]
    [InlineData("USER", "OELITE")]
    [InlineData("Username", "OELITE")]
    [InlineData("username", "oelite")]
    [InlineData("USERNAME", "OELITE")]
    public void ParseConnectionString_AcceptsUsernameAliasesRegardlessOfKeyCase(string key, string username)
    {
        var connectionString = $"Host=localhost;{key}={username};Password=secret";

        var parsed = ClickHouseConnectionWrapper.ParseConnectionString(connectionString);

        Assert.Contains($";Username={username};", parsed);
        Assert.Contains(";Password=secret;", parsed);
    }

    [Theory]
    [InlineData("Host=localhost;User=OELITE;Password=secret")]
    [InlineData("Host=localhost;username=oelite;password=secret")]
    [InlineData("clickhouse://oelite:secret@localhost:8123/default")]
    public void Constructor_DoesNotWriteCredentialsToConsole(string connectionString)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            using var connection = new ClickHouseConnectionWrapper(connectionString);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.DoesNotContain("secret", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oelite", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
