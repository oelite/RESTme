namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB field types - equivalent to RestmeDbColumnType
/// </summary>
public enum MongoFieldType
{
    /// <summary>
    /// Normal field - stored in document
    /// </summary>
    NormalField,

    /// <summary>
    /// ObjectId field - MongoDB's primary key
    /// </summary>
    ObjectId,

    /// <summary>
    /// Reference field - points to another document
    /// </summary>
    Reference,

    /// <summary>
    /// Embedded document field
    /// </summary>
    EmbeddedDocument,

    /// <summary>
    /// Array field
    /// </summary>
    Array,

    /// <summary>
    /// Computed field - not stored, calculated on query
    /// </summary>
    ComputedField,

    /// <summary>
    /// View field - from aggregation pipeline
    /// </summary>
    ViewField,

    /// <summary>
    /// Searchable text field
    /// </summary>
    SearchableText,

    /// <summary>
    /// Date field with automatic timestamps
    /// </summary>
    Timestamp
}
