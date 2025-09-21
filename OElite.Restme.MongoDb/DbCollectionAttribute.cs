using System;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom MongoDB collection attribute - equivalent to RestmeTable but for MongoDB
/// Specifies the collection name for MongoDB documents
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DbCollectionAttribute : Attribute
{
    public string CollectionName { get; }
    public DbNamingConvention NamingConvention { get; set; } = DbNamingConvention.SnakeCase;

    public DbCollectionAttribute(string? collectionName = null,
        DbNamingConvention namingConvention = DbNamingConvention.SnakeCase)
    {
        CollectionName = collectionName ?? string.Empty; // Will be set by the class name using naming convention
        NamingConvention = namingConvention;
    }

    /// <summary>
    /// Gets the collection name, using the class name with the specified naming convention if CollectionName is not provided
    /// </summary>
    /// <param name="className">The class name to convert</param>
    /// <returns>The collection name</returns>
    public string GetCollectionName(string className)
    {
        if (!string.IsNullOrEmpty(CollectionName))
        {
            return CollectionName;
        }

        return ConvertToNamingConvention(className, NamingConvention);
    }

    /// <summary>
    /// Converts a string to the specified naming convention
    /// </summary>
    /// <param name="input">The input string (typically a class name)</param>
    /// <param name="convention">The naming convention to apply</param>
    /// <returns>The converted string</returns>
    private static string ConvertToNamingConvention(string input, DbNamingConvention convention)
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
    private static string ToSnakeCase(string pascalCase)
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
    private static string ToCamelCase(string pascalCase)
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
}