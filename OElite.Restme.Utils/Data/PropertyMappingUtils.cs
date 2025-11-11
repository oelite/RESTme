using System;
using System.Reflection;
using OElite;

namespace OElite.Restme.Utils.Data;

/// <summary>
/// Utility class for property mapping operations
/// </summary>
public static class PropertyMappingUtils
{
    /// <summary>
    /// Gets the naming convention from the DbCollectionAttribute of the class
    /// Looks up the inheritance hierarchy to find a class with DbCollectionAttribute
    /// </summary>
    /// <param name="type">The class type</param>
    /// <returns>The naming convention, defaults to SnakeCase if not found</returns>
    public static DbNamingConvention GetNamingConventionFromClass(Type? type)
    {
        if (type == null)
        {
            return DbNamingConvention.SnakeCase;
        }

        // Look up the inheritance hierarchy to find a class with DbCollectionAttribute
        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var collectionAttr = currentType.GetCustomAttribute<DbCollectionAttribute>();
            if (collectionAttr != null)
            {
                return collectionAttr.NamingConvention;
            }

            currentType = currentType.BaseType;
        }

        // Default to SnakeCase if no DbCollectionAttribute is found
        return DbNamingConvention.SnakeCase;
    }

    /// <summary>
    /// Converts a string to the specified naming convention
    /// </summary>
    /// <param name="input">The input string (typically a field name)</param>
    /// <param name="convention">The naming convention to apply</param>
    /// <returns>The converted string</returns>
    public static string ConvertToNamingConvention(string input, DbNamingConvention convention)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return convention switch
        {
            DbNamingConvention.SnakeCase => ToSnakeCase(input),
            DbNamingConvention.CamelCase => ToCamelCase(input),
            DbNamingConvention.PascalCase => ToPascalCase(input),
            _ => ToSnakeCase(input)
        };
    }

    /// <summary>
    /// Converts snake_case to PascalCase
    /// </summary>
    public static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
        {
            return snakeCase;
        }

        var parts = snakeCase.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return snakeCase;
        }

        var result = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
            {
                result.Append(char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());
            }
        }

        return result.ToString();
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

        var result = new System.Text.StringBuilder();

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
    /// Converts PascalCase to camelCase for legacy database compatibility
    /// </summary>
    public static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        // If the first character is uppercase, make it lowercase
        if (char.IsUpper(pascalCase[0]))
        {
            return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
        }

        return pascalCase;
    }

    /// <summary>
    /// Checks if two types are compatible for property mapping
    /// </summary>
    /// <param name="sourceType">The source property type</param>
    /// <param name="targetType">The target property type</param>
    /// <returns>True if the types are compatible for mapping</returns>
    public static bool AreTypesCompatible(Type sourceType, Type targetType)
    {
        // Exact type match
        if (sourceType == targetType)
        {
            return true;
        }

        // Handle nullable types
        var sourceUnderlyingType = Nullable.GetUnderlyingType(sourceType);
        var targetUnderlyingType = Nullable.GetUnderlyingType(targetType);

        // If both are nullable, check underlying types
        if (sourceUnderlyingType != null && targetUnderlyingType != null)
        {
            return AreTypesCompatible(sourceUnderlyingType, targetUnderlyingType);
        }

        // If source is nullable and target is not, check if underlying type matches
        if (sourceUnderlyingType != null && targetUnderlyingType == null)
        {
            return AreTypesCompatible(sourceUnderlyingType, targetType);
        }

        // If target is nullable and source is not, check if source matches underlying type
        if (sourceUnderlyingType == null && targetUnderlyingType != null)
        {
            return AreTypesCompatible(sourceType, targetUnderlyingType);
        }

        // Handle inheritance
        if (targetType.IsAssignableFrom(sourceType))
        {
            return true;
        }

        // Handle common type conversions
        if (IsNumericType(sourceType) && IsNumericType(targetType))
        {
            return true;
        }

        // Handle string conversions
        if (sourceType == typeof(string) || targetType == typeof(string))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is a numeric type
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
               type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }
}
