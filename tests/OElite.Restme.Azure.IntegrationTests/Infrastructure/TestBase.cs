using DotNet.Testcontainers.Builders;
using OElite.Providers;
using Testcontainers.Azurite;

namespace OElite.Restme.Azure.IntegrationTests;

public class TestBase : IDisposable
{
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithPortBinding(10000, true) // Blob service
        .WithPortBinding(10001, true) // Queue service
        .WithPortBinding(10002, true) // Table service
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
        .Build();

    private RestConfig? Config;
    protected Rest Restme;

    public virtual async Task InitializeAsync()
    {
        await _azuriteContainer.StartAsync();

        Config = new RestConfig
        {
            ConnectionString =
                $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:{_azuriteContainer.GetMappedPublicPort(10000)}/devstoreaccount1;QueueEndpoint=http://localhost:{_azuriteContainer.GetMappedPublicPort(10001)}/devstoreaccount1;TableEndpoint=http://localhost:{_azuriteContainer.GetMappedPublicPort(10002)}/devstoreaccount1;",
            OperationMode = RestMode.Azure
        };
        Restme = new Rest(Config.ConnectionString, Config);
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
    }


    protected TestBase()
    {
        // Configuration will be set up in derived test classes
        InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _azuriteContainer?.DisposeAsync().ConfigureAwait(false);
    }
}