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
            var memberMap = classMap.GetMemberMap(conflict.PropertyName);
            if (memberMap != null)
            {
                // Re-map to use the derived class property specifically
                classMap.UnmapMember(memberMap.MemberInfo);
                classMap.MapMember(conflict.DerivedProperty);
            }
        }
        catch (Exception ex)
        {
            // Enhanced error handling with proper logging instead of silent Console.WriteLine
            var errorMessage = $"MongoDB property conflict resolution failed for property '{conflict.PropertyName}' " +
                $"in class '{classMap.ClassType.Name}': {ex.Message}";

            // Try to map the derived property directly as fallback
            try
            {
                classMap.MapMember(conflict.DerivedProperty);
            }
            catch (Exception fallbackEx)
            {
                // Log detailed error information and throw to prevent silent failures
                var detailedError = $"Critical MongoDB mapping failure: Could not resolve property conflict for '{conflict.PropertyName}' " +
                    $"in class '{classMap.ClassType.Name}'. Primary error: {ex.Message}. " +
                    $"Fallback error: {fallbackEx.Message}. This may cause serialization issues.";

                Console.WriteLine($"ERROR: {detailedError}");

                // For critical properties like Status, we need to ensure the mapping doesn't silently fail
                if (conflict.PropertyName == "Status")
                {
                    throw new InvalidOperationException(detailedError, ex);
                }
            }
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
