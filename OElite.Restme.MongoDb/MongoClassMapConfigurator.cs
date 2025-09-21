using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

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

            // Register our custom convention
            var conventionPack = new ConventionPack();
            conventionPack.Add(new RestmeDbAttributeConvention());
            ConventionRegistry.Register("RestmeDbConvention", conventionPack,
                t => typeof(BaseEntity).IsAssignableFrom(t));
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

/// <summary>
/// Custom convention that applies our RestmeDb attributes to MongoDB class mappings
/// </summary>
public class RestmeDbAttributeConvention : ConventionBase, IClassMapConvention
{
    public void Apply(BsonClassMap classMap)
    {
        var type = classMap.ClassType;

        // Only apply to BaseEntity types
        if (!typeof(BaseEntity).IsAssignableFrom(type))
            return;

        // Configure to ignore extra elements (fields that don't exist in the C# class)
        // This prevents deserialization errors when MongoDB documents have extra fields
        classMap.SetIgnoreExtraElements(true);

        // Configure collection name
        var collectionAttr = type.GetCustomAttribute<DbCollectionAttribute>();
        if (collectionAttr != null)
        {
            var collectionName = collectionAttr.GetCollectionName(type.Name);
            classMap.SetDiscriminator(collectionName);
        }

        // Configure properties
        var properties =
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.DeclaringType != type) continue;
            var fieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
            var idAttr = property.GetCustomAttribute<DbIdAttribute>();
            var ignoreAttr = property.GetCustomAttribute<DbFieldIgnore>();
            var dateTimeAttr = property.GetCustomAttribute<DbDateTimeOptionsAttribute>();

            // Skip ignored properties
            if (ignoreAttr != null)
            {
                classMap.UnmapProperty(property.Name);
                continue;
            }

            // Try to get existing member map first, then map if needed
            var memberMap = classMap.GetMemberMap(property.Name);
            if (memberMap == null)
            {
                // Try to get from base class if property is inherited
                if (property.DeclaringType != type && classMap.BaseClassMap != null)
                {
                    memberMap = classMap.BaseClassMap.GetMemberMap(property.Name);
                }
            }

            if (memberMap == null)
            {
                // Map the property - this should work now that base classes are mapped
                if (property.DeclaringType == type)
                {
                    // Property is declared in the current class
                    memberMap = classMap.MapProperty(property.Name);
                }
                else
                {
                    // Property is inherited - use MapField which works for inherited properties
                    memberMap = classMap.MapMember(property);
                }
            }

            // Apply custom field mappings
            if (memberMap != null)
            {
                if (idAttr != null)
                {
                    // This is the ID field, map to _id
                    memberMap.SetElementName("_id");

                    // For RestmeObjectId, explicitly set our custom serializer
                    if (property.PropertyType == typeof(DbObjectId))
                    {
                        memberMap.SetSerializer(new DbObjectIdSerializer());
                    }
                }
                else if (fieldAttr != null && !string.IsNullOrEmpty(fieldAttr.FieldName))
                {
                    // Use the explicit field name from the attribute
                    memberMap.SetElementName(fieldAttr.FieldName);
                }
                else
                {
                    // Apply naming convention from the class for properties without explicit field names
                    var fieldName = GetFieldNameFromNamingConvention(property.Name, collectionAttr);
                    memberMap.SetElementName(fieldName);
                }

                // Handle DateTime options
                if (dateTimeAttr != null)
                {
                    // Configure DateTime serialization with UTC kind
                    var dateTimeSerializer = new DateTimeSerializer(dateTimeAttr.Kind);
                    memberMap.SetSerializer(dateTimeSerializer);
                }

                // Set up custom deserializer for null value handling
                SetCustomDeserializer(memberMap, property.PropertyType);
            }
        }
    }

    /// <summary>
    /// Gets the field name using the naming convention from the class attribute
    /// </summary>
    /// <param name="propertyName">The property name to convert</param>
    /// <param name="collectionAttr">The DbCollectionAttribute from the class</param>
    /// <returns>The converted field name</returns>
    private static string GetFieldNameFromNamingConvention(string propertyName, DbCollectionAttribute? collectionAttr)
    {
        if (collectionAttr == null)
        {
            // If no DbCollectionAttribute is found, throw exception as required
            throw new InvalidOperationException(
                "Class must have DbCollectionAttribute to determine field naming convention");
        }

        return ConvertToNamingConvention(propertyName, collectionAttr.NamingConvention);
    }

    /// <summary>
    /// Converts a string to the specified naming convention
    /// </summary>
    /// <param name="input">The input string (typically a property name)</param>
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
    /// Converts PascalCase to snake_case for MongoDB field naming convention
    /// </summary>
    private static string ToSnakeCase(string pascalCase)
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

    /// <summary>
    /// Sets up custom deserializer for null value handling
    /// </summary>
    private static void SetCustomDeserializer(BsonMemberMap memberMap, Type propertyType)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        var actualType = underlyingType ?? propertyType;

        // Skip RestmeObjectId as it has its own custom serializer
        if (actualType == typeof(DbObjectId))
        {
            return;
        }

        // Skip DateTime properties with BsonDateTimeOptions attribute as they have their own serialization
        if (actualType == typeof(DateTime))
        {
            return;
        }

        // For value types (non-nullable), set up custom deserializer to handle null values
        if (actualType.IsValueType && !actualType.IsEnum && underlyingType == null)
        {
            var serializer = CreateNullHandlingSerializer(actualType);
            if (serializer != null)
            {
                memberMap.SetSerializer(serializer);
            }
        }
    }

    /// <summary>
    /// Creates a custom serializer that handles null values during deserialization
    /// </summary>
    private static IBsonSerializer? CreateNullHandlingSerializer(Type type)
    {
        if (type == typeof(long))
        {
            return new NullHandlingSerializer<long>(0L);
        }
        else if (type == typeof(int))
        {
            return new NullHandlingSerializer<int>(0);
        }
        else if (type == typeof(short))
        {
            return new NullHandlingSerializer<short>(0);
        }
        else if (type == typeof(byte))
        {
            return new NullHandlingSerializer<byte>(0);
        }
        else if (type == typeof(double))
        {
            return new NullHandlingSerializer<double>(0.0);
        }
        else if (type == typeof(float))
        {
            return new NullHandlingSerializer<float>(0.0f);
        }
        else if (type == typeof(decimal))
        {
            return new NullHandlingSerializer<decimal>(0.0m);
        }
        else if (type == typeof(bool))
        {
            return new NullHandlingSerializer<bool>(false);
        }
        else if (type == typeof(DateTime))
        {
            return new NullHandlingSerializer<DateTime>(DateTime.MinValue);
        }
        else if (type == typeof(Guid))
        {
            return new NullHandlingSerializer<Guid>(Guid.Empty);
        }
        else if (type == typeof(string))
        {
            return new NullHandlingSerializer<string>(string.Empty);
        }
        else if (type.IsEnum)
        {
            // For enums, use the first value (usually 0)
            var enumValues = Enum.GetValues(type);
            if (enumValues.Length > 0)
            {
                var defaultValue = enumValues.GetValue(0);
                if (defaultValue != null)
                {
                    return new NullHandlingSerializer<object>(defaultValue);
                }
            }
        }

        // For other types, return null (no custom serializer needed)
        return null;
    }
}

/// <summary>
/// Custom serializer that handles null values during deserialization by returning a default value
/// </summary>
public class NullHandlingSerializer<T> : IBsonSerializer<T>
{
    private readonly T _defaultValue;
    private readonly IBsonSerializer<T> _defaultSerializer;

    public NullHandlingSerializer(T defaultValue)
    {
        _defaultValue = defaultValue;
        _defaultSerializer = BsonSerializer.LookupSerializer<T>();
    }

    public Type ValueType => typeof(T);

    public T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();

        // If the value is null or missing, return the default value
        if (bsonType == BsonType.Null || bsonType == BsonType.Undefined)
        {
            context.Reader.ReadNull();
            return _defaultValue;
        }

        // Otherwise, use the default serializer
        return _defaultSerializer.Deserialize(context, args);
    }

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
    {
        _defaultSerializer.Serialize(context, args, value);
    }

    object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return Deserialize(context, args);
    }

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        Serialize(context, args, (T)value);
    }
}