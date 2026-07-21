using System;
using System.Threading.Tasks;
using FluentAssertions;
using OElite.Restme;
using OElite.Restme.Abstractions;
using OElite.Restme.RabbitMQ.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.RabbitMQ.IntegrationTests.Tests;

/// <summary>
/// Tests for provider disposal checking and automatic cleanup functionality
/// </summary>
[Collection("RabbitMQIntegration")]
public class ProviderDisposalTests : RabbitMQTestBase
{
    private readonly ITestOutputHelper _output;

    public ProviderDisposalTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetProvider_ShouldReturnSameCachedInstance()
    {
        // Arrange & Act
        var provider1 = Rest.GetProvider<IQueueProvider>();
        var provider2 = Rest.GetProvider<IQueueProvider>();

        // Assert
        provider1.Should().NotBeNull("First call should return a provider");
        provider2.Should().NotBeNull("Second call should return a provider");
        provider1.Should().BeSameAs(provider2, "Multiple GetProvider calls should return the same cached instance");

        _output.WriteLine($"✅ Both calls returned the same cached instance: {provider1?.GetType().Name}");
    }

    [Fact]
    public async Task GetProvider_ShouldCreateNewInstanceAfterDisposal()
    {
        // Arrange
        var originalProvider = Rest.GetProvider<IQueueProvider>();
        originalProvider.Should().NotBeNull("Original provider should be created");

        _output.WriteLine($"Original provider: {originalProvider?.GetType().Name}");

        // Act - Dispose the provider
        originalProvider?.Dispose();
        _output.WriteLine("Provider disposed");

        // Get provider again (should create new instance)
        var newProvider = Rest.GetProvider<IQueueProvider>();

        // Assert
        newProvider.Should().NotBeNull("New provider should be created after disposal");
        newProvider.Should().NotBeSameAs(originalProvider, "New provider should be a different instance");
        newProvider.Should().BeOfType<OElite.Providers.RabbitMQProvider>("New provider should be RabbitMQProvider");

        _output.WriteLine($"New provider: {newProvider?.GetType().Name}");
        _output.WriteLine("✅ Successfully created new instance after disposal");
    }

    [Fact]
    public async Task GetProvider_MultipleDisposalCycles_ShouldWork()
    {
        // Test multiple disposal cycles to ensure the mechanism is robust
        IQueueProvider? provider1 = null;
        IQueueProvider? provider2 = null;
        IQueueProvider? provider3 = null;

        try
        {
            // Cycle 1
            provider1 = Rest.GetProvider<IQueueProvider>();
            provider1.Should().NotBeNull("First provider should be created");
            _output.WriteLine($"Created provider1: {provider1?.GetType().Name}");

            provider1?.Dispose();
            _output.WriteLine("Disposed provider1");

            // Cycle 2
            provider2 = Rest.GetProvider<IQueueProvider>();
            provider2.Should().NotBeNull("Second provider should be created");
            provider2.Should().NotBeSameAs(provider1, "Second provider should be different from first");
            _output.WriteLine($"Created provider2: {provider2?.GetType().Name}");

            provider2?.Dispose();
            _output.WriteLine("Disposed provider2");

            // Cycle 3
            provider3 = Rest.GetProvider<IQueueProvider>();
            provider3.Should().NotBeNull("Third provider should be created");
            provider3.Should().NotBeSameAs(provider1, "Third provider should be different from first");
            provider3.Should().NotBeSameAs(provider2, "Third provider should be different from second");
            _output.WriteLine($"Created provider3: {provider3?.GetType().Name}");

            _output.WriteLine("✅ Multiple disposal cycles completed successfully");
        }
        finally
        {
            // Cleanup any remaining providers
            provider3?.Dispose();
        }
    }

    [Fact]
    public async Task GetProvider_DifferentProviderTypes_ShouldHandleIndependently()
    {
        // This test verifies that disposal checking works independently for different provider types
        // Since RabbitMQ only supports IQueueProvider, we'll test with different names

        var queueProvider1 = Rest.GetProvider<IQueueProvider>("queue1");
        var queueProvider2 = Rest.GetProvider<IQueueProvider>("queue2");

        queueProvider1.Should().NotBeNull("Queue provider 1 should be created");
        queueProvider2.Should().NotBeNull("Queue provider 2 should be created");
        queueProvider1.Should().NotBeSameAs(queueProvider2, "Different named providers should be different instances");

        _output.WriteLine($"Created queue1 provider: {queueProvider1?.GetType().Name}");
        _output.WriteLine($"Created queue2 provider: {queueProvider2?.GetType().Name}");

        // Dispose only one
        queueProvider1?.Dispose();
        _output.WriteLine("Disposed queue1 provider");

        // Get providers again
        var newQueueProvider1 = Rest.GetProvider<IQueueProvider>("queue1");
        var sameQueueProvider2 = Rest.GetProvider<IQueueProvider>("queue2");

        newQueueProvider1.Should().NotBeNull("New queue1 provider should be created");
        newQueueProvider1.Should().NotBeSameAs(queueProvider1, "New queue1 provider should be different");
        sameQueueProvider2.Should().BeSameAs(queueProvider2, "Queue2 provider should be the same (not disposed)");

        _output.WriteLine("✅ Independent disposal handling verified");

        // Cleanup
        newQueueProvider1?.Dispose();
        sameQueueProvider2?.Dispose();
    }
}