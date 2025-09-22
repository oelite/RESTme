using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB transaction interface that mimics SQL transaction pattern
/// </summary>
public interface IMongoTransaction : IDisposable
{
    /// <summary>
    /// Commits the transaction
    /// </summary>
    Task CommitAsync();

    /// <summary>
    /// Rolls back the transaction
    /// </summary>
    Task RollbackAsync();

    /// <summary>
    /// Gets the MongoDB client session for this transaction
    /// </summary>
    IClientSessionHandle Session { get; }

    /// <summary>
    /// Gets the transaction options
    /// </summary>
    TransactionOptions Options { get; }

    /// <summary>
    /// Gets whether the transaction is active
    /// </summary>
    bool IsActive { get; }
}