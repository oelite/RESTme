using OElite.Restme.Abstractions;
using Xunit;

namespace OElite.Restme.Tests
{
    /// <summary>
    /// Regression tests for issue #11 — Rest.LoadProviderAssemblies fallback must not
    /// wipe the process-global ServiceLocator factory registrations.
    /// </summary>
    public class ServiceLocatorPreservationTests
    {
        private const string TestFactoryName = "servicelocator-preservation-test";

        [Fact]
        public void ProviderLoadFailure_PreservesNamedRegistrations()
        {
            // GIVEN a named factory registered via the public API
            var factory = new StubServiceFactory();
            ServiceLocator.RegisterFactory(TestFactoryName, factory);

            // WHEN a Rest instance is created in a mode whose provider assembly load
            // fails (Redis assembly referenced here may or may not resolve; either way
            // the fallback path must not clear existing registrations)
            var config = new RestConfig(RestMode.Redis)
            {
                ConnectionString = "localhost:6399" // intentionally unreachable port
            };
            using var rest = new Rest(config);

            // THEN the named registration survives
            Assert.Same(factory, ServiceLocator.GetFactory(TestFactoryName));
        }

        [Fact]
        public void RepeatedHostCreation_PreservesNamedRegistrations()
        {
            // GIVEN a named registration and a first host build
            var factory = new StubServiceFactory();
            ServiceLocator.RegisterFactory(TestFactoryName, factory);

            var firstConfig = new RestConfig(RestMode.Redis) { ConnectionString = "localhost:6399" };
            using (var first = new Rest(firstConfig))
            {
                Assert.Same(factory, ServiceLocator.GetFactory(TestFactoryName));
            }

            // WHEN a second host build occurs in the same process
            var secondConfig = new RestConfig(RestMode.S3) { ConnectionString = "localhost:6399" };
            using (var second = new Rest(secondConfig))
            {
                // THEN the registration from before the first host still resolves
                Assert.Same(factory, ServiceLocator.GetFactory(TestFactoryName));
            }
        }

        private sealed class StubServiceFactory : IServiceFactory
        {
            public ProviderCapabilities SupportedCapabilities => ProviderCapabilities.None;

            public bool CanCreateProvider<T>() where T : class, IRestmeProvider => false;

            public T? CreateProvider<T>(RestConfig config) where T : class, IRestmeProvider => null;
            public ICacheProvider? CreateCacheProvider(RestConfig config) => null;
            public IQueueProvider? CreateQueueProvider(RestConfig config) => null;
            public IStorageProvider? CreateStorageProvider(RestConfig config) => null;
            public IHttpProvider? CreateHttpProvider(RestConfig config) => null;
            public ILogProvider? CreateLogProvider(RestConfig config) => null;
            public IColumnarProvider? CreateColumnarProvider(RestConfig config) => null;
            public IEventStreamProvider? CreateStreamingProvider(RestConfig config) => null;
            public ISearchProvider? CreateSearchProvider(RestConfig config) => null;
        }
    }
}
