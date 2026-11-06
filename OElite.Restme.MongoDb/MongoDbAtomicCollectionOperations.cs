using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

internal enum MongoDbAtomicInsertOutcome
{
    Inserted,
    DuplicateKey
}

internal interface IMongoDbAtomicCollectionOperations<T> where T : BaseEntity
{
    IBsonSerializer<T> DocumentSerializer { get; }
    Task<MongoDbAtomicInsertOutcome> InsertOneAsync(T document, CancellationToken cancellationToken);
    Task<ReplaceOneResult> ReplaceOneAsync(BsonDocument filter, T replacement, CancellationToken cancellationToken);
}

internal sealed class MongoDbAtomicCollectionOperations<T> : IMongoDbAtomicCollectionOperations<T> where T : BaseEntity
{
    private readonly IMongoCollection<T> _collection;

    internal MongoDbAtomicCollectionOperations(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public IBsonSerializer<T> DocumentSerializer => _collection.DocumentSerializer;

    public async Task<MongoDbAtomicInsertOutcome> InsertOneAsync(T document, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return MongoDbAtomicInsertOutcome.Inserted;
        }
        catch (MongoWriteException exception) when (MongoDbAtomicWrite.IsDuplicate(exception))
        {
            return MongoDbAtomicInsertOutcome.DuplicateKey;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MongoWriteConcernException)
        {
            throw new MongoDbWriteException(MongoDbWriteFailure.OutcomeUnknown);
        }
        catch (MongoException)
        {
            throw new MongoDbWriteException(MongoDbWriteFailure.ProviderFailure);
        }
    }

    public async Task<ReplaceOneResult> ReplaceOneAsync(BsonDocument filter, T replacement, CancellationToken cancellationToken)
    {
        try
        {
            return await _collection.ReplaceOneAsync(
                new BsonDocumentFilterDefinition<T>(filter), replacement, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MongoWriteConcernException)
        {
            throw new MongoDbWriteException(MongoDbWriteFailure.OutcomeUnknown);
        }
        catch (MongoException)
        {
            throw new MongoDbWriteException(MongoDbWriteFailure.ProviderFailure);
        }
    }
}
