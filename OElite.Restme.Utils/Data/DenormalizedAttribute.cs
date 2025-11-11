using System;
using System.Reflection;

namespace OElite;

/// <summary>
/// Common interface for denormalized attributes
/// </summary>
public abstract class DenormalizedAttribute : Attribute
{
    /// <summary>
    /// The source collection name where this denormalized data comes from
    /// </summary>
    public string FromCollection { get; set; } = string.Empty;

    /// <summary>
    /// If the collection is referenced multiple times for different target properties/fields,
    /// a referenceKey is required to ensure the correct mapping.
    /// </summary>
    public string? CollectionReference { get; set; }

    /// <summary>
    /// Enhanced reference key that supports:
    /// - @PropertyName: References property from current class (e.g., @CategoryId)
    /// - #PropertyName: References property from current property class if it's a class type (e.g., #CategoryId)
    /// </summary>
    public string? ReferenceKey { get; set; }

    /// <summary>
    /// Legacy property for backward compatibility
    /// </summary>
    public string? ReferencedByProperty => ReferenceKey;

    /// <summary>
    /// Query configuration for advanced denormalization
    /// </summary>
    public DbSimpleQuery? Query { get; set; }

    /// <summary>
    /// Determines if this attribute uses query-based denormalization
    /// </summary>
    public bool IsQueryBased => !string.IsNullOrWhiteSpace(Query?.Query);

    /// <summary>
    /// Determines if this attribute uses reference key-based denormalization
    /// </summary>
    public bool IsReferenceKeyBased => !IsQueryBased && !string.IsNullOrWhiteSpace(ReferenceKey);

    /// <summary>
    /// Determines if this attribute uses property-based denormalization (alias for IsReferenceKeyBased)
    /// </summary>
    public bool IsPropertyBased => IsReferenceKeyBased;

    /// <summary>
    /// Controls whether changes to the source entity should trigger cascade updates to entities that reference this field.
    /// Default is false to prevent mass updates and improve performance.
    /// Set to true only for fields where changes should propagate to referencing entities.
    /// </summary>
    public bool CascadeUpdate { get; set; } = false;

    /// <summary>
    /// Gets the processed MongoDB query with @ parameters ready for substitution
    /// and single quotes converted to double quotes
    /// </summary>
    public string? ProcessedQuery => ProcessQuery(Query?.Query);

    /// <summary>
    /// Gets the processed MongoDB sort specification with @ parameters ready for substitution
    /// and single quotes converted to double quotes
    /// </summary>
    public string? ProcessedSort => ProcessQuery(Query?.Sort);

    /// <summary>
    /// Gets the limit from the query configuration
    /// </summary>
    public int Limit => Query?.Limit ?? 10;

    /// <summary>
    /// Gets the sort specification from the query configuration
    /// </summary>
    public string? Sort => Query?.Sort;

    /// <summary>
    /// Processes a query string by converting single quotes to double quotes
    /// and normalizing whitespace for MongoDB compatibility
    /// </summary>
    /// <param name="query">The query string to process</param>
    /// <returns>Processed query string or null if input is null/empty</returns>
    private static string? ProcessQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        // Convert single quotes to double quotes for valid JSON
        // Handle escaped single quotes properly
        var processed = query
            .Replace("\\'", "___ESCAPED_QUOTE___") // Temporarily replace escaped quotes
            .Replace("'", "\"") // Convert single quotes to double quotes
            .Replace("___ESCAPED_QUOTE___", "'"); // Restore escaped quotes

        // Normalize whitespace (optional, helps with readability in logs)
        return global::System.Text.RegularExpressions.Regex.Replace(processed.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Converts a string to the specified naming convention
    /// </summary>
    /// <param name="input">The input string (typically a property name)</param>
    /// <param name="convention">The naming convention to apply</param>
    /// <returns>The converted string</returns>
    protected static string ConvertToNamingConvention(string input, DbNamingConvention convention)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return convention switch
        {
            DbNamingConvention.SnakeCase => ToSnakeCase(input),
            DbNamingConvention.CamelCase => ToCamelCase(input),
            DbNamingConvention.PascalCase => input, // Already PascalCase
            _ => ToSnakeCase(input)
        };
    }

    /// <summary>
    /// Converts PascalCase to snake_case
    /// </summary>
    public static string ToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        var result = new global::System.Text.StringBuilder();

        for (int i = 0; i < pascalCase.Length; i++)
        {
            char currentChar = pascalCase[i];

            // If this is an uppercase character and not the first character, add underscore
            if (char.IsUpper(currentChar) && i > 0)
            {
                result.Append('_');
            }

            // Convert to lowercase
            result.Append(char.ToLowerInvariant(currentChar));
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts PascalCase to camelCase
    /// </summary>
    protected static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        if (pascalCase.Length == 1)
        {
            return pascalCase.ToLowerInvariant();
        }

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }

    /// <summary>
    /// Parses a reference key string to extract property name and target field name
    /// Supports syntax: '@PropertyName', '@PropertyName as targetField', '#PropertyName', '#PropertyName as targetField'
    /// </summary>
    /// <param name="referenceKey">The reference key string to parse</param>
    /// <returns>Tuple of (propertyName, targetFieldName, isCurrentClass)</returns>
    public static (string propertyName, string? targetFieldName, bool isCurrentClass) ParseReferenceKey(
        string referenceKey)
    {
        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            return (referenceKey, null, false);
        }

        var trimmed = referenceKey.Trim();
        bool isCurrentClass = false;
        string propertyName;
        string? targetFieldName = null;

        // Parse @ syntax (current class property)
        if (trimmed.StartsWith("@"))
        {
            isCurrentClass = true;
            var content = trimmed.Substring(1).Trim();

            // Check for 'as targetField' syntax
            var asIndex = content.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
            if (asIndex > 0)
            {
                propertyName = content.Substring(0, asIndex).Trim();
                targetFieldName = content.Substring(asIndex + 4).Trim();
            }
            else
            {
                propertyName = content;
            }
        }
        // Parse # syntax (current property class property)
        else if (trimmed.StartsWith("#"))
        {
            isCurrentClass = false;
            var content = trimmed.Substring(1).Trim();

            // Check for 'as targetField' syntax
            var asIndex = content.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
            if (asIndex > 0)
            {
                propertyName = content.Substring(0, asIndex).Trim();
                targetFieldName = content.Substring(asIndex + 4).Trim();
            }
            else
            {
                propertyName = content;
            }
        }
        // Legacy support: direct property name
        else
        {
            isCurrentClass = true;
            propertyName = trimmed;
        }

        if (targetFieldName.IsNullOrEmpty())
        {
            targetFieldName = "_id";
        }

        return (propertyName, targetFieldName, isCurrentClass);
    }

    /// <summary>
    /// Gets the field name for a property, considering DbFieldAttribute if present
    /// </summary>
    /// <param name="property">The property to get the field name for</param>
    /// <returns>The field name to use in MongoDB queries</returns>
    protected static string GetFieldNameFromProperty(PropertyInfo property)
    {
        var dbFieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
        if (dbFieldAttr != null && !string.IsNullOrWhiteSpace(dbFieldAttr.FieldName))
        {
            return dbFieldAttr.FieldName;
        }

        // Convert property name to snake_case as default
        return ToSnakeCase(property.Name);
    }
}