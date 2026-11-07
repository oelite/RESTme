namespace OElite.Restme.MongoDb;

/// <summary>Typed result of a provider-neutral insert-if-absent operation.</summary>
public enum MongoDbInsertResultStatus
{
    Inserted,
    DuplicateKey
}

/// <summary>Result of an insert-if-absent operation. Never exposes a MongoDB driver type.</summary>
public sealed record MongoDbInsertResult<T>(MongoDbInsertResultStatus Status, T? Document, string? Reason)
    where T : BaseEntity;

/// <summary>Provider-owned version request for one atomic compare-and-swap replacement.</summary>
public sealed record MongoDbVersionedWrite<T>(
    T Replacement,
    string VersionProperty,
    long ExpectedVersion,
    System.Linq.Expressions.Expression<Func<T, bool>>? Condition = null)
    where T : BaseEntity;

/// <summary>Typed result of an expected-version compare-and-swap update.</summary>
public enum MongoDbCasResultStatus
{
    Updated,
    Conflict
}

/// <summary>Result of an expected-version CAS update. Never exposes a MongoDB driver type.</summary>
public sealed record MongoDbCasResult<T>(
        MongoDbCasResultStatus Status,
        T? Document,
        long? VersionBefore,
        long? VersionAfter,
        string? Reason)
    where T : BaseEntity;

/// <summary>Stable provider-level write classifications.</summary>
public enum MongoDbTransactionSupport
{
    Unsupported
}

/// <summary>Provider-level transaction failure classifications.</summary>
public enum MongoDbTransactionFailure
{
    Unsupported,
    Aborted,
    OutcomeUnknown
}

/// <summary>Provider-level transaction errors without driver details.</summary>
public sealed class MongoDbTransactionException : Exception
{
    public MongoDbTransactionFailure Failure { get; }

    public MongoDbTransactionException(MongoDbTransactionFailure failure)
        : base($"Collection transaction failed: {failure}.")
    {
        Failure = failure;
    }
}

public enum MongoDbWriteFailure
{
    Unsupported,
    DuplicateKey,
    ProviderFailure,
    OutcomeUnknown
}

/// <summary>Failure classification for an ordered bulk insert.</summary>
public enum MongoDbBulkInsertFailure
{
    DuplicateKey,
    ProviderFailure,
    OutcomeUnknown
}

/// <summary>
/// Sanitized failure from an ordered bulk insert. InsertedCount is the count confirmed by the
/// provider as successful before the failure; for OutcomeUnknown it is not a complete count.
/// Reconcile before retrying when OutcomeUnknown is reported.
/// </summary>
public sealed class MongoDbBulkInsertException : Exception
{
    public MongoDbBulkInsertFailure Failure { get; }
    public int InsertedCount { get; }

    public MongoDbBulkInsertException(MongoDbBulkInsertFailure failure, int insertedCount)
        : base($"Bulk insert failed: {failure}.")
    {
        Failure = failure;
        InsertedCount = insertedCount;
    }
}

/// <summary>
/// Failure from a new atomic write API. OutcomeUnknown means the outcome is not asserted;
/// reconcile before retrying. Driver messages, collection details, and credentials are never retained.
/// </summary>
public sealed class MongoDbWriteException : Exception
{
    public MongoDbWriteFailure Failure { get; }

    public MongoDbWriteException(MongoDbWriteFailure failure)
        : base($"Collection write failed: {failure}.")
    {
        Failure = failure;
    }
}
