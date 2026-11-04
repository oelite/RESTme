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

            // Per-shadowed-type serialization provider (restme#12 note 63578): entities that
            // declare their own Status property are serialized by ShadowedStatusSerializer<T>,
            // which suppresses the inherited BaseEntity.Status element per derived type WITHOUT
            // mutating the shared frozen base class map. Registration order is therefore
            // irrelevant. Non-shadow types resolve null and use the normal class-map path.
            BsonSerializer.RegisterSerializationProvider(new RestmeShadowSerializationProvider());

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

            // Shadowed types (declare their own Status) are owned by the serialization provider
            // (ShadowedStatusSerializer<T>), which composes a PRIVATE class map internally.
            // No class map is registered in the global BsonClassMap registry for shadowed types —
            // this is what makes Freeze() duplicate-element errors impossible by construction
            // and keeps the shared BaseEntity map untouched (restme#12 note 63578).
            if (RestmeShadowSerializationProviderApplies(typeof(T)))
            {
                _registeredTypes.Add(typeof(T));
                return;
            }

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

                    // Resolve property conflicts (non-Status shadowed properties only —
                    // Status shadows never reach this path, see above)
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
    /// Ensures all base classes are mapped before mapping the derived class.
    /// Base class maps are ALWAYS registered with their own Status member (previously the
    /// order-dependent UnmapStatusFrom/SuppressInheritedStatusIfNeeded mutations of the shared
    /// BaseEntity map are deleted — restme#12 note 63578). This method should only be
    /// called from within a lock.
    /// </summary>
    private static void EnsureBaseClassesMapped<T>() where T : BaseEntity
    {
        // First, ensure BaseEntity itself is mapped — always WITH Status; shadow resolution
        // happens exclusively per-derived-type in ShadowedStatusSerializer<T>.
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

        // Register base classes in order (most derived first).
        // Intermediate base classes that themselves shadow Status are owned by the
        // serialization provider, like leaf shadowed types — skip class-map registration.
        baseTypes.Reverse();
        foreach (var baseType in baseTypes)
        {
            if (!_registeredTypes.Contains(baseType) && !BsonClassMap.IsClassMapRegistered(baseType))
            {
                if (RestmeShadowSerializationProviderApplies(baseType))
                {
                    _registeredTypes.Add(baseType);
                    continue;
                }

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
    /// True when the type declares its own Status property (shadowing BaseEntity.Status) and
    /// is therefore owned by <see cref="RestmeShadowSerializationProvider"/> rather than by a
    /// registered class map.
    /// </summary>
    private static bool RestmeShadowSerializationProviderApplies(Type type)
    {
        return type != typeof(BaseEntity) &&
               typeof(BaseEntity).IsAssignableFrom(type) &&
               type.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null;
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

            // Shadowed types are owned by the serialization provider — no class-map registration.
            if (RestmeShadowSerializationProviderApplies(type))
            {
                _registeredTypes.Add(type);
                configuredTypes.Add(type);
                return;
            }

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
