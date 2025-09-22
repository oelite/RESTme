using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB transaction implementation
/// </summary>
public class MongoTransaction : IMongoTransaction
{
    private readonly IClientSessionHandle _session;
    private readonly TransactionOptions _options;
    private bool _disposed = false;
    private bool _committed = false;
    private bool _rolledBack = false;

    public MongoTransaction(IClientSessionHandle session, TransactionOptions options = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _options = options ?? new TransactionOptions();

        // Start the transaction
        _session.StartTransaction(_options);
    }

    public IClientSessionHandle Session => _session;
    public TransactionOptions Options => _options;
    public bool IsActive => _session.IsInTransaction && !_committed && !_rolledBack;

    public async Task CommitAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MongoTransaction));

        if (_committed)
            throw new InvalidOperationException("Transaction has already been committed");

        if (_rolledBack)
            throw new InvalidOperationException("Transaction has already been rolled back");

        if (!_session.IsInTransaction)
            throw new InvalidOperationException("No active transaction to commit");

        await _session.CommitTransactionAsync();
        _committed = true;
    }

    public async Task RollbackAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MongoTransaction));

        if (_committed)
            throw new InvalidOperationException("Transaction has already been committed");

        if (_rolledBack)
            throw new InvalidOperationException("Transaction has already been rolled back");

        if (_session.IsInTransaction)
        {
            await _session.AbortTransactionAsync();
        }

        _rolledBack = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // If transaction is still active and not committed/rolled back, roll it back
                if (IsActive)
                {
                    try
                    {
                        _session.AbortTransaction();
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }

                _session?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
