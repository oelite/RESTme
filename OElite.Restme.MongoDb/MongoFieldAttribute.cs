namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB field attribute - equivalent to RestmeDbColumn
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MongoFieldAttribute : Attribute
{
    public string? FieldName { get; set; }
    public MongoFieldType FieldType { get; set; } = MongoFieldType.NormalField;
    public bool IsIndexed { get; set; } = false;
    public bool IsRequired { get; set; } = false;
    public object? DefaultValue { get; set; }

    public MongoFieldAttribute()
    {
    }

    public MongoFieldAttribute(string fieldName)
    {
        FieldName = fieldName;
    }

    public MongoFieldAttribute(string fieldName, MongoFieldType fieldType)
    {
        FieldName = fieldName;
        FieldType = fieldType;
    }
}

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