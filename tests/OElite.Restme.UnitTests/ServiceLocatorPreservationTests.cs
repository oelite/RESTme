using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OElite.Restme.Abstractions;
using Xunit;

namespace OElite.Restme.Tests
{
    /// <summary>
    /// Regression tests for issue #11 — Rest.LoadProviderAssemblies fallback must not
    /// wipe the process-global ServiceLocator factory registrations.
    ///
    /// The fallback branch (which contained the old ServiceLocator.Clear() call) is only
    /// reached when LoadAssembly fails. On developer machines a populated NuGet cache makes
    /// the provider assembly load succeed, so the fallback would never run and these tests
    /// would pass vacuously. The internal Rest test constructor injects an assembly-loader
    /// override that forces the failure deterministically; each test also asserts via the
    /// captured ILogger that the fallback actually ran ("Failed to load provider assembly"
    /// warning), converting any future vacuous pass into a hard failure.
    /// </summary>
    public class ServiceLocatorPreservationTests
    {
        private const string TestFactoryName = "servicelocator-preservation-test";

        [Fact]
        public void ProviderLoadFailure_PreservesTypedAndNamedRegistrations()
        {
            // GIVEN typed and named factories registered via the public API
            var namedFactory = new StubServiceFactory();
            var typedFactory = new StubServiceFactory();
            ServiceLocator.RegisterFactory(TestFactoryName, namedFactory);
            ServiceLocator.RegisterFactory<MarkerType>(typedFactory);

            // WHEN a Rest instance is created and the provider assembly load fails
            var logger = new CaptureLogger();
            using var rest = CreateRestWithForcedLoadFailure(RestMode.Redis, logger);

            // THEN the fallback path actually ran (guards against vacuous pass)
            Assert.Contains(
                logger.Events,
                e => e.Level >= LogLevel.Warning && e.Message.Contains("Failed to load provider assembly"));

            // AND both the named and typed registrations survive
            Assert.Same(namedFactory, ServiceLocator.GetFactory(TestFactoryName));
            Assert.Same(typedFactory, ServiceLocator.GetFactory<MarkerType>());
        }

        [Fact]
        public void SecondHost_PreservesRegistrations_AndRedisS3FactoriesResolve()
        {
            // GIVEN static-ctor-equivalent registrations for redis/s3 plus a foreign
            // named registration, and a first host build whose provider load fails
            var redisFactory = new StubServiceFactory();
            var s3Factory = new StubServiceFactory();
            var foreignFactory = new StubServiceFactory();
            ServiceLocator.RegisterFactory("redis", redisFactory);
            ServiceLocator.RegisterFactory("s3", s3Factory);
            ServiceLocator.RegisterFactory(TestFactoryName, foreignFactory);

            var firstLogger = new CaptureLogger();
            using (var first = CreateRestWithForcedLoadFailure(RestMode.Redis, firstLogger))
            {
                Assert.Contains(
                    firstLogger.Events,
                    e => e.Message.Contains("Failed to load provider assembly"));
            }

            // WHEN a second host builds in the same process (S3 mode, load also forced to fail)
            var secondLogger = new CaptureLogger();
            using (var second = CreateRestWithForcedLoadFailure(RestMode.S3, secondLogger))
            {
                // THEN the fallback ran on the second host too, and factory resolution
                // still returns non-null for redis and s3 (the "Cache provider is null"
                // symptom from issue #11 cannot recur)
                Assert.Contains(
                    secondLogger.Events,
                    e => e.Message.Contains("Failed to load provider assembly"));

                Assert.Same(redisFactory, ServiceLocator.GetFactory("redis"));
                Assert.Same(s3Factory, ServiceLocator.GetFactory("s3"));
                Assert.Same(foreignFactory, ServiceLocator.GetFactory(TestFactoryName));
            }
        }

        private static Rest CreateRestWithForcedLoadFailure(RestMode mode, ILogger logger)
        {
            return new Rest(new RestConfig(mode) { ConnectionString = "localhost:6399" }, logger,
                _ => false); // force LoadAssembly to "fail" so the fallback branch runs
        }

        private sealed class MarkerType
        {
        }

        private sealed class CaptureLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Events { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Events.Add((logLevel, formatter(state, exception)));
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
