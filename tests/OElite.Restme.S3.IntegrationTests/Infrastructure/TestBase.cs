using OElite.Providers;

namespace OElite.Restme.S3.IntegrationTests;

public class TestBase : IDisposable
{
    protected RestConfig Config { get; }
    protected S3StorageProvider StorageProvider { get; }

    protected TestBase()
    {
        // Configuration will be set up in derived test classes
        Config = new RestConfig();
        StorageProvider = new S3StorageProvider(Config);
    }

    public void Dispose()
    {
        StorageProvider?.Dispose();
    }
}