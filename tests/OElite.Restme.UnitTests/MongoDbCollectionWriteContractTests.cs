using System;
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;
using OElite.Restme.MongoDb;
using Xunit;

namespace OElite.Restme.UnitTests;

[Trait("Category", "Unit")]
public class MongoDbCollectionWriteContractTests
{
    private sealed class VersionedEntity : BaseEntity
    {
        [MongoDB.Bson.Serialization.Attributes.BsonElement("version_number")]
        public long Version { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Test-only injected seam for the two new atomic-write methods. It replaces only the driver
    // calls those methods use; legacy methods still delegate to the real collection. A production
    // collection built with this constructor receives a neutral placeholder IMongoCollection<T>
    // through a null-forgiving reference and never touches it, so tests cannot accidentally claim
    // database behavior.
    private sealed class InjectedAtomicOperations : IMongoDbAtomicCollectionOperations<VersionedEntity>
    {
        public IBsonSerializer<VersionedEntity> DocumentSerializer { get; } =
            BsonSerializer.SerializerRegistry.GetSerializer<VersionedEntity>();

        public Func<VersionedEntity, CancellationToken, Task<MongoDbAtomicInsertOutcome>>? InsertOneAsyncOverride { get; set; }
        public Func<BsonDocument, VersionedEntity, CancellationToken, Task<ReplaceOneResult>>? ReplaceOneAsyncOverride { get; set; }

        public Task<MongoDbAtomicInsertOutcome> InsertOneAsync(
            VersionedEntity document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InsertOneAsyncOverride is null
                ? Task.FromResult(MongoDbAtomicInsertOutcome.Inserted)
                : InsertOneAsyncOverride(document, cancellationToken);
        }

        public Task<ReplaceOneResult> ReplaceOneAsync(
            BsonDocument filter, VersionedEntity replacement, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReplaceOneAsyncOverride is null
                ? Task.FromResult<ReplaceOneResult>(ReplaceOneResult.Unacknowledged.Instance)
                : ReplaceOneAsyncOverride(filter, replacement, cancellationToken);
        }
    }

    // Instantiate IMongoDbCollection<T> whose atomic methods use the injected overrides while
    // preserving its compile-time non-null production path for the real collection field.
    private static IMongoDbCollection<VersionedEntity> CreateCollection(
        InjectedAtomicOperations ops)
    {
        // null-forgiving placeholder: production semantics require a real collection; tests only
        // exercise atomic methods, which never read _collection.
        IMongoCollection<VersionedEntity> placeholder = null!;
        return new MongoDbCollectionImplementation<VersionedEntity>(placeholder, ops);
    }

    // ── Public-interface contract tests ────────────────────────────────────────

    [Fact]
    public async Task InsertOneIfAbsentAsync_Inserted_DispatchesThroughPublicInterface()
    {
        var ops = new InjectedAtomicOperations();
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);
        var entity = new VersionedEntity { Id = DbObjectId.NewId(), Version = 5 };

        var result = await collection.InsertOneIfAbsentAsync(entity);

        Assert.Equal(MongoDbInsertResultStatus.Inserted, result.Status);
        Assert.Same(entity, result.Document);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task InsertOneIfAbsentAsync_DuplicateKey_FromProviderReturnsTypedOutcome()
    {
        var ops = new InjectedAtomicOperations
        {
            InsertOneAsyncOverride = (doc, ct) =>
                Task.FromResult(MongoDbAtomicInsertOutcome.DuplicateKey)
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        var result = await collection.InsertOneIfAbsentAsync(new VersionedEntity { Id = DbObjectId.NewId() });

        Assert.Equal(MongoDbInsertResultStatus.DuplicateKey, result.Status);
        Assert.Null(result.Document);
        Assert.Equal("Unique key already exists.", result.Reason);
    }

    [Fact]
    public async Task InsertOneIfAbsentAsync_NonDuplicateProviderError_SanitizedWithoutLeak()
    {
        var ops = new InjectedAtomicOperations
        {
            InsertOneAsyncOverride = (doc, ct) =>
                throw new MongoDbWriteException(MongoDbWriteFailure.OutcomeUnknown)
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        var error = await Assert.ThrowsAsync<MongoDbWriteException>(() =>
            collection.InsertOneIfAbsentAsync(new VersionedEntity { Id = DbObjectId.NewId() }));

        Assert.Equal(MongoDbWriteFailure.OutcomeUnknown, error.Failure);
    }

    [Fact]
    public async Task InsertOneIfAbsentAsync_Cancellation_NoWriteAttempt()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var called = false;
        var ops = new InjectedAtomicOperations
        {
            InsertOneAsyncOverride = (doc, ct) => { called = true; throw new OperationCanceledException(ct); }
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            collection.InsertOneIfAbsentAsync(new VersionedEntity { Id = DbObjectId.NewId() }, cancellationToken: source.Token));

        Assert.False(called);
    }

    // ── CAS / version contract ──────────────────────────────────────────────

    [Fact]
    public async Task ReplaceOneIfVersionAsync_Updated_WithExactVersionIncrement()
    {
        var ops = new InjectedAtomicOperations
        {
            ReplaceOneAsyncOverride = (filter, repl, ct) =>
                Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null))
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);
        var entity = new VersionedEntity { Id = DbObjectId.NewId(), Version = 2 };

        var result = await collection.ReplaceOneIfVersionAsync(entity, x => x.Version, 2);

        // The caller object is never mutated by the provider-side contract.
        Assert.Equal(2L, entity.Version);
        Assert.Equal(MongoDbCasResultStatus.Updated, result.Status);
        Assert.Equal(2L, result.VersionBefore);
        Assert.Equal(3L, result.VersionAfter);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task ReplaceOneIfVersionAsync_Updated_AssertsMappedElementName()
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<VersionedEntity>();
        var documentSerializer = Assert.IsAssignableFrom<IBsonDocumentSerializer>(serializer);
        Assert.True(documentSerializer.TryGetMemberSerializationInfo(nameof(VersionedEntity.Version), out var versionInfo));
        Assert.NotEqual(nameof(VersionedEntity.Version), versionInfo.ElementName);

        var ops = new InjectedAtomicOperations();
        ops.ReplaceOneAsyncOverride = (filter, repl, ct) =>
        {
            Assert.True(filter.ToString().Contains(versionInfo.ElementName, StringComparison.Ordinal),
                "CAS filter must target serializer-mapped element, not CLR name");
            Assert.False(filter.ToString().Contains("\"Version\"", StringComparison.Ordinal),
                "CAS filter must not use CLR property name");
            var replacementDocument = repl.ToBsonDocument(serializer);
            Assert.True(replacementDocument.Contains(versionInfo.ElementName),
                "CAS replacement must carry serializer-mapped element");
            return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null));
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        var result = await collection.ReplaceOneIfVersionAsync(
            new VersionedEntity { Id = DbObjectId.NewId(), Version = 2 },
            x => x.Version,
            2);

        Assert.Equal(MongoDbCasResultStatus.Updated, result.Status);
        Assert.Equal(3L, result.VersionAfter);
    }

    [Fact]
    public async Task ReplaceOneIfVersionAsync_Conflict_ZeroMatch_NoRacyRead()
    {
        var replaceCalls = 0;
        var ops = new InjectedAtomicOperations
        {
            ReplaceOneAsyncOverride = (filter, repl, ct) =>
            {
                replaceCalls++;
                return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(0, 0, null));
            }
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        var result = await collection.ReplaceOneIfVersionAsync(
            new VersionedEntity { Id = DbObjectId.NewId(), Version = 5 }, x => x.Version, 5);

        Assert.Equal(MongoDbCasResultStatus.Conflict, result.Status);
        Assert.Null(result.VersionBefore);
        Assert.Null(result.VersionAfter);
        Assert.Equal("Conditional write did not match.", result.Reason);
        // Single conditional replace, no follow-up count/read of any kind.
        Assert.Equal(1, replaceCalls);
    }

    [Fact]
    public async Task ReplaceOneIfVersionAsync_OutcomeUnknown_Unhandled()
    {
        var ops = new InjectedAtomicOperations
        {
            ReplaceOneAsyncOverride = (filter, repl, ct) =>
                Task.FromResult<ReplaceOneResult>(ReplaceOneResult.Unacknowledged.Instance)
        };
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);

        await Assert.ThrowsAsync<MongoDbWriteException>(() =>
            collection.ReplaceOneIfVersionAsync(
                new VersionedEntity { Id = DbObjectId.NewId(), Version = 5 }, x => x.Version, 5));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ThrowsUnsupported_NoCallbackExecution()
    {
        var ops = new InjectedAtomicOperations();
        IMongoDbCollection<VersionedEntity> collection = CreateCollection(ops);
        var invoked = false;

        var error = await Assert.ThrowsAsync<MongoDbTransactionException>(() =>
            collection.ExecuteInTransactionAsync<object>(_ =>
            {
                invoked = true;
                return Task.FromResult(new object());
            }));

        Assert.Equal(MongoDbTransactionFailure.Unsupported, error.Failure);
        Assert.False(invoked);
    }

    [Fact]
    public void ProviderContract_NoMongoDriverTypesInNewSignatures()
    {
        var methods = typeof(IMongoDbCollection<VersionedEntity>).GetMethods();
        foreach (var m in methods)
        {
            string? rt = m.ReturnType.FullName;
            if (rt != null && rt.Contains("MongoDB.Driver", StringComparison.Ordinal))
                throw new Exception($"{m.Name} leaks MongoDB.Driver in return type");
            foreach (var p in m.GetParameters())
            {
                string? pt = p.ParameterType.FullName;
                if (pt != null && pt.Contains("MongoDB.Driver", StringComparison.Ordinal))
                    throw new Exception($"{m.Name} parameter {p.Name} leaks MongoDB.Driver");
            }
        }
        foreach (var t in new[]
        {
            typeof(MongoDbInsertResult<VersionedEntity>),
            typeof(MongoDbCasResult<VersionedEntity>),
            typeof(MongoDbVersionedWrite<VersionedEntity>)
        })
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string? pt = p.PropertyType.FullName;
                if (pt != null && pt.Contains("MongoDB.Driver", StringComparison.Ordinal))
                    throw new Exception($"{t.Name}.{p.Name} leaks MongoDB.Driver");
            }
        }
    }
}