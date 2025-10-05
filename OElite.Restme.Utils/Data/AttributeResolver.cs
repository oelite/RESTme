using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OElite.Restme.Utils.Data;

/// <summary>
/// Generic attribute resolver for handling property conflicts and attribute-based logic
/// </summary>
public static class AttributeResolver
{
    /// <summary>
    /// Gets all properties that should be excluded from serialization based on attributes
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of property names that should be excluded</returns>
    public static IEnumerable<string> GetExcludedProperties(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var excludedProperties = new List<string>();

        foreach (var property in properties)
        {
            // Check for denormalized attributes
            var denormalizedAttr = property.GetCustomAttribute<DenormalizedAttribute>();
            if (denormalizedAttr != null)
            {
                excludedProperties.Add(property.Name);
                continue;
            }

            // Check for ignore attributes
            var ignoreAttr = property.GetCustomAttribute<DbFieldIgnore>();
            if (ignoreAttr != null)
            {
                excludedProperties.Add(property.Name);
            }
        }

        return excludedProperties;
    }

    /// <summary>
    /// Gets properties that have conflicts with base class properties (using 'new' keyword)
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of property conflict information</returns>
    public static IEnumerable<PropertyConflict> GetPropertyConflicts(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var baseType = type.BaseType;
        var conflicts = new List<PropertyConflict>();

        if (baseType == null) return conflicts;

        foreach (var property in properties)
        {
            // Check if this property has the 'new' keyword (hides a base class property)
            if (property.DeclaringType == type)
            {
                var baseProperty = baseType.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
                if (baseProperty != null)
                {
                    conflicts.Add(new PropertyConflict
                    {
                        PropertyName = property.Name,
                        DerivedProperty = property,
                        BaseProperty = baseProperty,
                        ConflictType = PropertyConflictType.HiddenBaseProperty
                    });
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Gets all properties with specific attribute types
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for</typeparam>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of properties with the specified attribute</returns>
    public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<TAttribute>(Type type) 
        where TAttribute : Attribute
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties.Where(p => p.GetCustomAttribute<TAttribute>() != null);
    }

    /// <summary>
    /// Gets all properties with specific attribute types and their attribute instances
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to search for</typeparam>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of property-attribute pairs</returns>
    public static IEnumerable<(PropertyInfo Property, TAttribute Attribute)> GetPropertiesWithAttributeInstances<TAttribute>(Type type) 
        where TAttribute : Attribute
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<TAttribute>()))
            .Where(pair => pair.Attribute != null)!;
    }

    /// <summary>
    /// Gets the inheritance chain for a type (excluding object)
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of base types in order from most derived to least derived</returns>
    public static IEnumerable<Type> GetInheritanceChain(Type type)
    {
        var baseTypes = new List<Type>();
        var currentType = type.BaseType;
        
        while (currentType != null && currentType != typeof(object))
        {
            baseTypes.Add(currentType);
            currentType = currentType.BaseType;
        }

        // Return in reverse order (most derived first)
        baseTypes.Reverse();
        return baseTypes;
    }
}

/// <summary>
/// Represents a property conflict between base and derived classes
/// </summary>
public class PropertyConflict
{
    public string PropertyName { get; set; } = string.Empty;
    public PropertyInfo DerivedProperty { get; set; } = null!;
    public PropertyInfo BaseProperty { get; set; } = null!;
    public PropertyConflictType ConflictType { get; set; }
}

/// <summary>
/// Types of property conflicts
/// </summary>
public enum PropertyConflictType
{
    HiddenBaseProperty,
    DuplicateFieldName,
    TypeMismatch
}
