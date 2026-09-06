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
                t => true);
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
                    // Only map properties declared directly on this type.
                    // Inherited properties are handled by the base class maps.
                    // AutoMap is intentionally NOT called here because it recreates
                    // inherited member maps that conflict with base class registrations.
                    var convention = new RestmeDbAttributeConvention();
                    convention.Apply(cm);

                    // Resolve property conflicts (e.g., Status hiding base Status)
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
        // Collect all derived types in the chain that shadow BaseEntity.Status with 'new'.
        // Shadowing types must be detected BEFORE registering BaseEntity so the registration action
        // can unmap Status after AutoMap() and convention apply, but before the class map freezes.
        var shadowingTypes = CollectShadowingTypes(typeof(T));

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
                    if (shadowingTypes.Count > 0)
                    {
                        UnmapStatusFrom(cm);
                    }
                });
                _registeredTypes.Add(typeof(BaseEntity));
            }
            catch (ArgumentException ex) when (ex.Message.Contains("An item with the same key has already been added"))
            {
                // Another thread already registered BaseEntity - this is fine
                _registeredTypes.Add(typeof(BaseEntity));
            }
        }

        SuppressInheritedStatusIfNeeded(typeof(T));

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
                        if (shadowingTypes.Contains(baseType))
                        {
                            UnmapStatusFrom(cm);
                        }
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

            SuppressInheritedStatusIfNeeded(baseType);
        }
    }

    private static bool HasShadowedStatus(Type type)
    {
        // DeclaredOnly avoids AmbiguousMatchException when multiple Status properties exist in the hierarchy.
        var statusProperty = type.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        return statusProperty != null;
    }

    /// <summary>
    /// Collects all types in the inheritance chain from <paramref name="derivedType"/> up to
    /// (but not including) BaseEntity that shadow BaseEntity.Status with a 'new' keyword.
    /// </summary>
    private static HashSet<Type> CollectShadowingTypes(Type derivedType)
    {
        var shadowing = new HashSet<Type>();
        var current = derivedType;
        while (current != null && current != typeof(BaseEntity) && typeof(BaseEntity).IsAssignableFrom(current))
        {
            if (HasShadowedStatus(current))
            {
                shadowing.Add(current);
            }
            current = current.BaseType;
        }
        return shadowing;
    }

    /// <summary>
    /// Removes the Status member from the given class map. Must be called BEFORE AutoMap()
    /// because AutoMap freezes the map, after which UnmapProperty is a no-op.
    /// </summary>
    private static void UnmapStatusFrom(BsonClassMap classMap)
    {
        var statusMember = classMap.GetMemberMap("Status");
        if (statusMember != null)
        {
            classMap.UnmapProperty("Status");
        }
    }

    /// <summary>
    /// If the given type shadows BaseEntity.Status with a 'new' keyword, unmaps the inherited
    /// BaseEntity.Status member from the BaseEntity class map so only the shadow property is serialized.
    /// This is inferred automatically from the presence of a shadowed Status property — no attribute required.
    /// </summary>
    private static void SuppressInheritedStatusIfNeeded(Type type)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
            return;

        var baseClassMap = BsonClassMap.LookupClassMap(typeof(BaseEntity));
        if (baseClassMap == null || baseClassMap.IsFrozen)
            return;

        // A shadowed property is one declared directly on `type`; an inherited one comes from BaseEntity.
        // DeclaredOnly avoids AmbiguousMatchException when multiple Status properties exist in the hierarchy.
        var shadowedStatus = type.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (shadowedStatus == null)
            return;

        UnmapStatusFrom(baseClassMap);
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

                    // Only map properties declared directly on this type.
                    // AutoMap is intentionally NOT called to avoid recreating
                    // inherited member maps that conflict with base class maps.
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

    public static void ResetForTesting()
    {
        lock (_lock)
        {
            _isConfigured = false;
            _registeredTypes.Clear();

            // Clear BsonClassMap registry via reflection (no public unregister API exists).
            var classMapsField = typeof(BsonClassMap).GetField("__classMaps", BindingFlags.NonPublic | BindingFlags.Static);
            if (classMapsField?.GetValue(null) is System.Collections.IDictionary classMaps)
            {
                classMaps.Clear();
            }
        }
    }
}