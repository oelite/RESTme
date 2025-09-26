using System;
using System.Reflection;

namespace OElite;

/// <summary>
/// Attribute to mark a property as a denormalized field from another collection
/// Inherits from DenormalizedCollectionAttribute but adds field-specific filtering
/// Supports both traditional field extraction and advanced query-based denormalization
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DenormalizedFieldAttribute : DenormalizedCollectionAttribute
{
    /// <summary>
    /// The source field name in the denormalized collection for value extraction.
    /// Used for simple value type denormalization (string, long, int, decimal, bool, DateTime, etc.).
    /// If "*" is specified, the entire document is returned as an object.
    /// If a specific field name is provided, only that field's value is extracted.
    /// </summary>
    public string FromField { get; set; } = string.Empty;


    /// <summary>
    /// Determines if this attribute uses field-based denormalization (not query-based)
    /// </summary>
    public bool IsFieldBased => !IsQueryBased && !string.IsNullOrWhiteSpace(FromField);

    /// <summary>
    /// Determines if this field should return the entire document as an object
    /// </summary>
    public bool IsEntireDocument => FromField == "*";

    /// <summary>
    /// Gets the FromField name, using the property name with the naming convention from the associated class if FromField is not specified
    /// </summary>
    /// <param name="propertyInfo">The property info to get the associated class and property name</param>
    /// <returns>The FromField name</returns>
    public string GetFromField(PropertyInfo propertyInfo)
    {
        if (!string.IsNullOrEmpty(FromField) && FromField != "*")
        {
            return FromField;
        }

        // Get the naming convention from the associated class
        var declaringType = propertyInfo.DeclaringType;
        if (declaringType == null)
        {
            throw new InvalidOperationException(
                $"Cannot determine declaring type for property {propertyInfo.Name}");
        }

        var collectionAttr = declaringType.GetCustomAttribute<DbCollectionAttribute>();
        if (collectionAttr == null)
        {
            throw new InvalidOperationException(
                $"Class {declaringType.Name} must have DbCollectionAttribute to use DenormalizedFieldAttribute without explicit FromField");
        }

        return ConvertToNamingConvention(propertyInfo.Name, collectionAttr.NamingConvention);
    }

    /// <summary>
    /// Validates that the property type is compatible with the FromField specification
    /// </summary>
    /// <param name="property">The property to validate</param>
    public void ValidatePropertyType(PropertyInfo property)
    {
        if (IsEntireDocument)
        {
            // If FromField is "*", the property must be a class type
            if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
            {
                throw new InvalidOperationException(
                    $"Property '{property.Name}' must be a class type when FromField is '*' (entire document). " +
                    $"Current type: {property.PropertyType.Name}");
            }
        }
    }


    /// <summary>
    /// Constructor for field denormalization
    /// </summary>
    /// <param name="fromCollection">The source collection name</param>
    /// <param name="fromField">The source field name ("*" for entire document, specific field name for field extraction)</param>
    /// <param name="referenceKey">Enhanced reference key using @ for current class properties or # for current property class properties</param>
    /// <param name="collectionReference">If the mapped collection in fromCollection are sourced multiple times in referencing record, a collection reference key is required</param>
    /// <param name="query">MongoDB query string with @ parameter substitution</param>
    /// <param name="sort">MongoDB sort specification</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cascadeUpdate">Whether changes to the source entity should trigger cascade updates to referencing entities. Default is false.</param>
    public DenormalizedFieldAttribute(string fromCollection, string fromField = "*", string referenceKey = "#Id",
        string? collectionReference = null, string? query = null, string? sort = null, int limit = 1, bool cascadeUpdate = false)
        : base(fromCollection, referenceKey, collectionReference, query, sort, limit)
    {
        if (string.IsNullOrWhiteSpace(fromField))
        {
            throw new ArgumentException("FromField is required for field denormalization", nameof(fromField));
        }

        if (fromField == "*" && (string.IsNullOrEmpty(referenceKey) ||
                                 (!referenceKey.StartsWith("#") && !referenceKey.StartsWith("@"))))
        {
            throw new ArgumentException(
                "When FromField is '*', ReferenceKey must be specified and start with '#' or '@'",
                nameof(referenceKey));
        }

        FromField = fromField;
        CascadeUpdate = cascadeUpdate;
    }
}