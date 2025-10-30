using System.Reflection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Configures MongoDB class mappings to use our custom attributes
/// </summary>
public static class MongoClassMapConfigurator
{
    private static bool _isConfigured = false;
    private static readonly object _lock = new object();
    private static readonly HashSet<Type> _registeredTypes = new HashSet<Type>();

    /// <summary>
    /// Configures MongoDB class mappings for all BaseEntity types
    /// </summary>
    public static void ConfigureClassMappings()
    {
        if (_isConfigured) return;

        lock (_lock)
        {
            if (_isConfigured) return;

            // Register our custom convention for all types to support embedded documents
            var conventionPack = new ConventionPack();
            conventionPack.Add(new RestmeDbAttributeConvention());
            ConventionRegistry.Register("RestmeDbConvention", conventionPack,
                t => true); // Apply to all types to support embedded documents
            _isConfigured = true;
        }
    }

    /// <summary>
    /// Explicitly registers class mapping for a specific type
    /// </summary>
    public static void RegisterClassMapping<T>() where T : BaseEntity
    {
        lock (_lock)
        {
            if (_registeredTypes.Contains(typeof(T)) || BsonClassMap.IsClassMapRegistered(typeof(T)))
                return;

            // First, ensure all base classes are mapped
            EnsureBaseClassesMapped<T>();

            try
            {
                BsonClassMap.RegisterClassMap<T>(cm =>
                {
                    // Use AutoMap first to handle inheritance properly
                    cm.AutoMap();

                    // Then apply our custom attribute mappings
                    var convention = new RestmeDbAttributeConvention();
                    convention.Apply(cm);

                    // Finally, resolve property conflicts
                    MongoPropertyConflictResolver.ResolvePropertyConflicts(cm, typeof(T));
                });

                _registeredTypes.Add(typeof(T));
            }
            catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added"))
            {
                // Another thread already registered this type - this is fine
                _registeredTypes.Add(typeof(T));
            }
        }
    }

    /// <summary>
    /// Ensures all base classes are mapped before mapping the derived class
    /// This method should only be called from within a lock
    /// </summary>
    private static void EnsureBaseClassesMapped<T>() where T : BaseEntity
    {
        // First, ensure BaseEntity itself is mapped
        if (!_registeredTypes.Contains(typeof(BaseEntity)) && !BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
        {
            try
            {
                BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
                {
                    cm.AutoMap();
                    var convention = new RestmeDbAttributeConvention();
                    convention.Apply(cm);
                });
                _registeredTypes.Add(typeof(BaseEntity));
            }
            catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added"))
            {
                // Another thread already registered BaseEntity - this is fine
                _registeredTypes.Add(typeof(BaseEntity));
            }
        }

        var currentType = typeof(T);
        var baseTypes = new List<Type>();

        // Collect all base types that inherit from BaseEntity (excluding BaseEntity itself)
        while (currentType != null && currentType != typeof(BaseEntity) &&
               typeof(BaseEntity).IsAssignableFrom(currentType))
        {
            currentType = currentType.BaseType;
            if (currentType != null && currentType != typeof(BaseEntity) &&
                typeof(BaseEntity).IsAssignableFrom(currentType))
            {
                baseTypes.Add(currentType);
            }
        }

        // Register base classes in order (most derived first)
        baseTypes.Reverse();
        foreach (var baseType in baseTypes)
        {
            if (!_registeredTypes.Contains(baseType) && !BsonClassMap.IsClassMapRegistered(baseType))
            {
                try
                {
                    // Use reflection to call the generic RegisterClassMap method
                    var registerMethod = typeof(BsonClassMap).GetMethod("RegisterClassMap", new[] { typeof(Action<BsonClassMap>) });
                    var genericMethod = registerMethod.MakeGenericMethod(baseType);
                    var action = new Action<BsonClassMap>(cm =>
                    {
                        cm.AutoMap();
                        var convention = new RestmeDbAttributeConvention();
                        convention.Apply(cm);
                        MongoPropertyConflictResolver.ResolvePropertyConflicts(cm, baseType);
                    });
                    genericMethod.Invoke(null, new object[] { action });
                    _registeredTypes.Add(baseType);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added"))
                {
                    // Another thread already registered this type - this is fine
                    _registeredTypes.Add(baseType);
                }
            }
        }
    }

    /// <summary>
    /// Configures class mapping for any type (not just BaseEntity) with conflict resolution
    /// This method is designed for use by repository classes that need to configure legacy types
    /// </summary>
    /// <param name="type">The type to configure</param>
    /// <param name="configuredTypes">Set of already configured types to avoid duplicate work</param>
    public static void ConfigureClassMappingForType(Type type, HashSet<Type> configuredTypes)
    {
        lock (_lock)
        {
            if (configuredTypes.Contains(type) || _registeredTypes.Contains(type))
                return;

            // Ensure all base classes are configured first
            MongoPropertyConflictResolver.EnsureBaseClassesConfigured(type, configuredTypes);

            // Configure the main type
            if (!BsonClassMap.IsClassMapRegistered(type))
            {
                try
                {
                    // Create a new BsonClassMap and register it manually
                    var classMap = new BsonClassMap(type);
                    BsonClassMap.RegisterClassMap(classMap);
                    classMap.AutoMap();
                    var convention = new RestmeDbAttributeConvention();
                    convention.Apply(classMap);
                    MongoPropertyConflictResolver.ResolvePropertyConflicts(classMap, type);
                    _registeredTypes.Add(type);
                }
                catch (InvalidOperationException)
                {
                    // Class map already registered by another thread
                    _registeredTypes.Add(type);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added"))
                {
                    // Another thread already registered this type - this is fine
                    _registeredTypes.Add(type);
                }
            }

            configuredTypes.Add(type);
        }
    }
}