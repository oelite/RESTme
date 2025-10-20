using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using OElite.Restme.Utils.Data;

namespace OElite.Restme.MongoDb;

/// <summary>
/// High-performance update builder that provides a fluent API for MongoDB update operations
/// Designed to be faster and more convenient than direct MongoDB.Driver Builders usage
/// </summary>
public class UpdateBuilder<T> where T : BaseEntity
{
    private readonly List<UpdateDefinition<T>> _updates = new();
    private readonly Dictionary<string, BsonValue> _setOperations = new();
    private readonly Dictionary<string, BsonValue> _incOperations = new();
    private readonly Dictionary<string, BsonValue> _pushOperations = new();
    private readonly Dictionary<string, BsonValue> _pullOperations = new();
    private readonly List<string> _unsetOperations = new();
    private bool _isOptimized = true;

    /// <summary>
    /// Set a field to a specific value using strongly-typed expression
    /// </summary>
    public UpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
    {
        var fieldName = GetFieldName(field);
        _setOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(value);
        return this;
    }

    /// <summary>
    /// Set multiple fields at once for better performance
    /// </summary>
    public UpdateBuilder<T> Set(params (Expression<Func<T, object>> field, object value)[] updates)
    {
        foreach (var (field, value) in updates)
        {
            var fieldName = GetFieldName(field);
            _setOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(value);
        }
        return this;
    }

    /// <summary>
    /// Increment a numeric field by a specific value
    /// </summary>
    public UpdateBuilder<T> Inc<TField>(Expression<Func<T, TField>> field, TField value) where TField : struct
    {
        var fieldName = GetFieldName(field);
        _incOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(value);
        return this;
    }

    /// <summary>
    /// Multiply a numeric field by a specific value
    /// </summary>
    public UpdateBuilder<T> Mul<TField>(Expression<Func<T, TField>> field, TField value) where TField : struct
    {
        var fieldName = GetFieldName(field);
        _updates.Add(Builders<T>.Update.Mul(field, value));
        _isOptimized = false; // Fall back to MongoDB builders for complex operations
        return this;
    }

    /// <summary>
    /// Set field to current date/time
    /// </summary>
    public UpdateBuilder<T> CurrentDate(Expression<Func<T, DateTime>> field)
    {
        var fieldName = GetFieldName(field);
        _setOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(DateTime.UtcNow);
        return this;
    }

    /// <summary>
    /// Unset (remove) a field from the document
    /// </summary>
    public UpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field)
    {
        var fieldName = GetFieldName(field);
        _unsetOperations.Add(fieldName);
        return this;
    }

    /// <summary>
    /// Push a value to an array field
    /// </summary>
    public UpdateBuilder<T> Push<TField>(Expression<Func<T, IEnumerable<TField>>> field, TField value)
    {
        var fieldName = GetFieldName(field);
        _pushOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(value);
        return this;
    }

    /// <summary>
    /// Push multiple values to an array field
    /// </summary>
    public UpdateBuilder<T> PushEach<TField>(Expression<Func<T, IEnumerable<TField>>> field, IEnumerable<TField> values)
    {
        var fieldName = GetFieldName(field);
        var bsonArray = new BsonArray(values.Select(v => MongoDbCollectionImplementation.ConvertToBsonValue(v)));
        _pushOperations[fieldName] = new BsonDocument("$each", bsonArray);
        return this;
    }

    /// <summary>
    /// Pull a value from an array field
    /// </summary>
    public UpdateBuilder<T> Pull<TField>(Expression<Func<T, IEnumerable<TField>>> field, TField value)
    {
        var fieldName = GetFieldName(field);
        _pullOperations[fieldName] = MongoDbCollectionImplementation.ConvertToBsonValue(value);
        return this;
    }

    /// <summary>
    /// Pull all values matching a condition from an array field
    /// </summary>
    public UpdateBuilder<T> PullAll<TField>(Expression<Func<T, IEnumerable<TField>>> field, IEnumerable<TField> values)
    {
        var fieldName = GetFieldName(field);
        var bsonArray = new BsonArray(values.Select(v => MongoDbCollectionImplementation.ConvertToBsonValue(v)));
        _pullOperations[fieldName] = bsonArray;
        return this;
    }

    /// <summary>
    /// Add to set (add value to array only if it doesn't exist)
    /// </summary>
    public UpdateBuilder<T> AddToSet<TField>(Expression<Func<T, IEnumerable<TField>>> field, TField value)
    {
        var fieldName = GetFieldName(field);
        _updates.Add(Builders<T>.Update.AddToSet(field, value));
        _isOptimized = false;
        return this;
    }

    /// <summary>
    /// Set value only if it's greater than current value
    /// </summary>
    public UpdateBuilder<T> Max<TField>(Expression<Func<T, TField>> field, TField value) where TField : IComparable<TField>
    {
        _updates.Add(Builders<T>.Update.Max(field, value));
        _isOptimized = false;
        return this;
    }

    /// <summary>
    /// Set value only if it's less than current value
    /// </summary>
    public UpdateBuilder<T> Min<TField>(Expression<Func<T, TField>> field, TField value) where TField : IComparable<TField>
    {
        _updates.Add(Builders<T>.Update.Min(field, value));
        _isOptimized = false;
        return this;
    }

    /// <summary>
    /// Builds the final UpdateDefinition with optimal performance
    /// Uses raw BSON operations when possible for maximum speed
    /// </summary>
    public UpdateDefinition<T> Build()
    {
        if (_isOptimized && _updates.Count == 0)
        {
            // Use optimized BsonDocument approach for simple operations
            return BuildOptimized();
        }
        else
        {
            // Fall back to MongoDB builders for complex operations
            return BuildWithBuilders();
        }
    }

    /// <summary>
    /// Builds using optimized BsonDocument operations for maximum performance
    /// This is significantly faster than using Builders for simple set/inc operations
    /// </summary>
    private UpdateDefinition<T> BuildOptimized()
    {
        var updateDoc = new BsonDocument();

        // Add $set operations
        if (_setOperations.Count > 0)
        {
            updateDoc["$set"] = new BsonDocument(_setOperations);
        }

        // Add $inc operations
        if (_incOperations.Count > 0)
        {
            updateDoc["$inc"] = new BsonDocument(_incOperations);
        }

        // Add $push operations
        if (_pushOperations.Count > 0)
        {
            updateDoc["$push"] = new BsonDocument(_pushOperations);
        }

        // Add $pull operations
        if (_pullOperations.Count > 0)
        {
            updateDoc["$pull"] = new BsonDocument(_pullOperations);
        }

        // Add $unset operations
        if (_unsetOperations.Count > 0)
        {
            var unsetDoc = new BsonDocument();
            foreach (var field in _unsetOperations)
            {
                unsetDoc[field] = "";
            }
            updateDoc["$unset"] = unsetDoc;
        }

        return new BsonDocumentUpdateDefinition<T>(updateDoc);
    }

    /// <summary>
    /// Builds using MongoDB Builders for complex operations
    /// </summary>
    private UpdateDefinition<T> BuildWithBuilders()
    {
        var allUpdates = new List<UpdateDefinition<T>>();

        // Convert optimized operations to Builders operations
        foreach (var (field, value) in _setOperations)
        {
            allUpdates.Add(Builders<T>.Update.Set(field, value));
        }

        foreach (var (field, value) in _incOperations)
        {
            allUpdates.Add(Builders<T>.Update.Inc(field, value));
        }

        foreach (var (field, value) in _pushOperations)
        {
            if (value.IsBsonDocument && value.AsBsonDocument.Contains("$each"))
            {
                var values = value.AsBsonDocument["$each"].AsBsonArray;
                allUpdates.Add(Builders<T>.Update.PushEach(field, values));
            }
            else
            {
                allUpdates.Add(Builders<T>.Update.Push(field, value));
            }
        }

        foreach (var (field, value) in _pullOperations)
        {
            if (value.IsBsonArray)
            {
                allUpdates.Add(Builders<T>.Update.PullAll(field, value.AsBsonArray));
            }
            else
            {
                allUpdates.Add(Builders<T>.Update.Pull(field, value));
            }
        }

        foreach (var field in _unsetOperations)
        {
            allUpdates.Add(Builders<T>.Update.Unset(field));
        }

        // Add any complex operations added via other methods
        allUpdates.AddRange(_updates);

        return allUpdates.Count == 1 ? allUpdates[0] : Builders<T>.Update.Combine(allUpdates);
    }

    /// <summary>
    /// Gets the MongoDB field name from a LINQ expression with caching for performance
    /// </summary>
    private static readonly Dictionary<string, string> _fieldNameCache = new();

    private static string GetFieldName<TField>(Expression<Func<T, TField>> field)
    {
        var expressionKey = field.ToString();

        if (_fieldNameCache.TryGetValue(expressionKey, out var cachedName))
        {
            return cachedName;
        }

        string fieldName;

        // Handle simple property access: x => x.Property
        if (field.Body is MemberExpression memberExpression)
        {
            fieldName = memberExpression.Member.Name;
        }
        // Handle convert expressions: x => (object)x.Property
        else if (field.Body is UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression &&
                 unaryExpression.Operand is MemberExpression convertedMember)
        {
            fieldName = convertedMember.Member.Name;
        }
        // Handle nested property access: x => x.Address.Street
        else if (field.Body is MemberExpression nestedMember)
        {
            var names = new List<string>();
            var current = nestedMember;

            while (current != null)
            {
                names.Insert(0, current.Member.Name);
                current = current.Expression as MemberExpression;
            }

            fieldName = string.Join(".", names);
        }
        else
        {
            // Fallback: use MongoDB's field name resolution
            fieldName = field.Body.ToString().Split('.').Last();
        }

        // Apply MongoDB naming conventions if needed
        fieldName = ApplyNamingConventions(fieldName);

        _fieldNameCache[expressionKey] = fieldName;
        return fieldName;
    }

    /// <summary>
    /// Applies MongoDB naming conventions (e.g., camelCase)
    /// </summary>
    private static string ApplyNamingConventions(string fieldName)
    {
        // Check if there's a custom field mapping attribute
        var property = typeof(T).GetProperty(fieldName);
        if (property != null)
        {
            // Check for MongoDB BsonElement attribute
            var bsonElementAttr = property.GetCustomAttribute<MongoDB.Bson.Serialization.Attributes.BsonElementAttribute>();
            if (bsonElementAttr != null && !string.IsNullOrEmpty(bsonElementAttr.ElementName))
            {
                return bsonElementAttr.ElementName;
            }

            // Check for custom field attributes from OElite.Restme
            var fieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
            if (fieldAttr != null && !string.IsNullOrEmpty(fieldAttr.FieldName))
            {
                return fieldAttr.FieldName;
            }
        }

        // Apply default camelCase convention
        return char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
    }
}

/// <summary>
/// Static factory for creating UpdateBuilder instances with a fluent API
/// </summary>
public static class Update
{
    /// <summary>
    /// Creates a new UpdateBuilder for the specified entity type
    /// </summary>
    public static UpdateBuilder<T> For<T>() where T : BaseEntity
    {
        return new UpdateBuilder<T>();
    }

    /// <summary>
    /// Creates an UpdateBuilder and immediately sets a field value
    /// </summary>
    public static UpdateBuilder<T> Set<T, TField>(Expression<Func<T, TField>> field, TField value) where T : BaseEntity
    {
        return new UpdateBuilder<T>().Set(field, value);
    }

    /// <summary>
    /// Creates an UpdateBuilder and immediately increments a field
    /// </summary>
    public static UpdateBuilder<T> Inc<T, TField>(Expression<Func<T, TField>> field, TField value) where T : BaseEntity where TField : struct
    {
        return new UpdateBuilder<T>().Inc(field, value);
    }

    /// <summary>
    /// Creates an UpdateBuilder for common timestamp updates
    /// </summary>
    public static UpdateBuilder<T> Timestamp<T>(Expression<Func<T, DateTime>> updatedField) where T : BaseEntity
    {
        return new UpdateBuilder<T>().Set(updatedField, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates an UpdateBuilder for common timestamp updates with multiple fields
    /// </summary>
    public static UpdateBuilder<T> Timestamps<T>(
        Expression<Func<T, DateTime>> updatedField,
        Expression<Func<T, DateTime>>? accessedField = null) where T : BaseEntity
    {
        var builder = new UpdateBuilder<T>().Set(updatedField, DateTime.UtcNow);

        if (accessedField != null)
        {
            builder.Set(accessedField, DateTime.UtcNow);
        }

        return builder;
    }
}