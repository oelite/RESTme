using System.Reflection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using OElite;
using OElite.Restme.Utils.Data;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Custom convention that applies our RestmeDb attributes to MongoDB class mappings
/// </summary>
public class RestmeDbAttributeConvention : ConventionBase, IClassMapConvention
{
    public void Apply(BsonClassMap classMap)
    {
        var type = classMap.ClassType;

        // Apply to all types for comprehensive naming convention support
        // This includes BaseEntity types and embedded document classes

        // Configure to ignore extra elements (fields that don't exist in the C# class)
        // This prevents deserialization errors when MongoDB documents have extra fields
        classMap.SetIgnoreExtraElements(true);

        // Configure collection name (only for BaseEntity types that are actual collections)
        var collectionAttr = type.GetCustomAttribute<OElite.DbCollectionAttribute>();
        if (collectionAttr != null && typeof(BaseEntity).IsAssignableFrom(type))
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
            var denormalizedAttr = property.GetCustomAttribute<DenormalizedAttribute>();
            

            // Skip properties marked with [DbFieldIgnore] - they are populated by DataPopulationService, not MongoDB serialization
            if (ignoreAttr != null)
            {
                classMap.UnmapProperty(property.Name);
                continue;
            }

            // Note: Denormalized properties without [DbFieldIgnore] will be serialized to MongoDB
            // This allows target entities (like Merchant) to persist denormalized fields to the database

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
                    // For embedded classes without DbCollection attribute, inherit from parent or use snake_case
                    var fieldName = GetFieldNameFromNamingConvention(property.Name, collectionAttr, type);
                    memberMap.SetElementName(fieldName);
                    
                }

                // Handle DateTime options
                if (dateTimeAttr != null)
                {
                    // Configure DateTime serialization with UTC kind
                    if (property.PropertyType == typeof(DateTime))
                    {
                        var dateTimeSerializer = new DateTimeSerializer(dateTimeAttr.Kind);
                        memberMap.SetSerializer(dateTimeSerializer);
                    }
                    else if (property.PropertyType == typeof(DateTime?))
                    {
                        // For nullable DateTime, use NullableSerializer with DateTimeSerializer
                        var dateTimeSerializer = new DateTimeSerializer(dateTimeAttr.Kind);
                        var nullableDateTimeSerializer = new NullableSerializer<DateTime>(dateTimeSerializer);
                        memberMap.SetSerializer(nullableDateTimeSerializer);
                    }
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
    /// <param name="type">The class type for inheritance resolution</param>
    /// <returns>The converted field name</returns>
    private static string GetFieldNameFromNamingConvention(string propertyName, OElite.DbCollectionAttribute? collectionAttr, Type type)
    {
        if (collectionAttr == null)
        {
            // For embedded classes without DbCollectionAttribute, try to find naming convention from property context
            // or default to snake_case convention for consistency with the platform
            var inheritedConvention = PropertyMappingUtils.GetNamingConventionFromClass(type);
            return PropertyMappingUtils.ConvertToNamingConvention(propertyName, inheritedConvention);
        }

        return PropertyMappingUtils.ConvertToNamingConvention(propertyName, collectionAttr.NamingConvention);
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
