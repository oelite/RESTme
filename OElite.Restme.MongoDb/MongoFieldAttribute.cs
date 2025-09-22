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