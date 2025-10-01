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
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            // First, ensure all base classes are mapped
            EnsureBaseClassesMapped<T>();

            BsonClassMap.RegisterClassMap<T>(cm =>
            {
                // Use AutoMap first to handle inheritance properly
                cm.AutoMap();

                // Then apply our custom attribute mappings
                var convention = new RestmeDbAttributeConvention();
                convention.Apply(cm);
            });
        }
    }

    /// <summary>
    /// Ensures all base classes are mapped before mapping the derived class
    /// </summary>
    private static void EnsureBaseClassesMapped<T>() where T : BaseEntity
    {
        // First, ensure BaseEntity itself is mapped
        if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
        {
            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                var convention = new RestmeDbAttributeConvention();
                convention.Apply(cm);
            });
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
            if (!BsonClassMap.IsClassMapRegistered(baseType))
            {
                // Use reflection to call the generic RegisterClassMap method
                var registerMethod = typeof(BsonClassMap).GetMethod("RegisterClassMap", new[] { typeof(Action<>) });
                var genericMethod = registerMethod.MakeGenericMethod(baseType);
                var action = new Action<BsonClassMap>(cm =>
                {
                    cm.AutoMap();
                    var convention = new RestmeDbAttributeConvention();
                    convention.Apply(cm);
                });
                genericMethod.Invoke(null, new object[] { action });
            }
        }
    }
}