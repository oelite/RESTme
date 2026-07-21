using System.Reflection;
using MongoDB.Bson.Serialization;
using OElite.Restme.Utils.Data;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Resolves property conflicts specifically for MongoDB class mapping
/// </summary>
public static class MongoPropertyConflictResolver
{
    /// <summary>
    /// Resolves property conflicts by preferring child class properties over parent class properties
    /// and unmapping denormalized properties
    /// </summary>
    /// <param name="classMap">The MongoDB class map to configure</param>
    /// <param name="type">The type being mapped</param>
    public static void ResolvePropertyConflicts(BsonClassMap classMap, Type type)
    {
        // Note: Denormalized properties are handled by RestmeDbAttributeConvention
        // This method only handles property conflicts (new keyword hiding base properties)
        
        var conflicts = AttributeResolver.GetPropertyConflicts(type);
        foreach (var conflict in conflicts)
        {
            ResolvePropertyConflict(classMap, conflict);
        }
    }

    /// <summary>
    /// Resolves a specific property conflict
    /// </summary>
    /// <param name="classMap">The MongoDB class map</param>
    /// <param name="conflict">The property conflict to resolve</param>
    private static void ResolvePropertyConflict(BsonClassMap classMap, PropertyConflict conflict)
    {
        try
        {
            // For shadowed properties (new keyword), map the derived property with a unique element name
            // The base property cannot be unmapped from base class maps, so we need a different approach
            var derivedMemberMap = classMap.GetMemberMap(conflict.DerivedProperty.Name);
            if (derivedMemberMap == null)
            {
                derivedMemberMap = classMap.MapMember(conflict.DerivedProperty);
            }

            // Use a unique element name to avoid conflict with base class property
            // Format: derivedtype_propertyname (e.g., tenant_status)
            var derivedTypeName = classMap.ClassType.Name.ToLowerInvariant();
            var uniqueElementName = $"{derivedTypeName}_{conflict.PropertyName.ToLowerInvariant()}";
            derivedMemberMap.SetElementName(uniqueElementName);
        }
        catch (Exception ex)
        {
            var errorMessage = $"MongoDB property conflict resolution failed for property '{conflict.PropertyName}' " +
                $"in class '{classMap.ClassType.Name}': {ex.Message}";
            Console.WriteLine($"ERROR: {errorMessage}");
        }
    }

    /// <summary>
    /// Configures a class map with proper conflict resolution for a type and its inheritance chain
    /// </summary>
    /// <param name="classMap">The class map to configure</param>
    /// <param name="type">The type to configure</param>
    public static void ConfigureClassMapWithConflictResolution(BsonClassMap classMap, Type type)
    {
        // First, auto-map the class
        classMap.AutoMap();
        
        // Then resolve conflicts
        ResolvePropertyConflicts(classMap, type);
    }

    /// <summary>
    /// Ensures all base classes in the inheritance chain are properly configured
    /// </summary>
    /// <param name="type">The type to configure</param>
    /// <param name="configuredTypes">Set of already configured types</param>
    public static void EnsureBaseClassesConfigured(Type type, HashSet<Type> configuredTypes)
    {
        var baseTypes = AttributeResolver.GetInheritanceChain(type);
        
        foreach (var baseType in baseTypes)
        {
            if (!configuredTypes.Contains(baseType))
            {
                try
                {
                    if (!BsonClassMap.IsClassMapRegistered(baseType))
                    {
                        var baseClassMap = new BsonClassMap(baseType);
                        BsonClassMap.RegisterClassMap(baseClassMap);
                        ConfigureClassMapWithConflictResolution(baseClassMap, baseType);
                    }
                    else
                    {
                        // If already registered, try to get the class map and resolve conflicts
                        var existingClassMap = BsonClassMap.LookupClassMap(baseType);
                        if (existingClassMap != null && !existingClassMap.IsFrozen)
                        {
                            ResolvePropertyConflicts(existingClassMap, baseType);
                        }
                    }
                    configuredTypes.Add(baseType);
                }
                catch (InvalidOperationException)
                {
                    // Class map already registered by another thread
                }
            }
        }
    }
}
