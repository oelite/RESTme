using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OElite;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Converts LINQ expressions to MongoDB aggregation pipeline stages
/// </summary>
public static class ExpressionToMongoPipeline
{
    /// <summary>
    /// Converts a projection expression to MongoDB $project stage
    /// </summary>
    public static BsonDocument CreateProjectStage<T, TResult>(Expression<Func<T, TResult>> selector) where T : BaseEntity
    {
        var projection = new BsonDocument();
        
        if (selector.Body is NewExpression newExpression)
        {
            // Handle new object creation: new { Name = p.Name, Price = p.Price }
            for (int i = 0; i < newExpression.Arguments.Count; i++)
            {
                var argument = newExpression.Arguments[i];
                var member = newExpression.Members?[i];
                
                if (member != null && argument is MemberExpression memberExpr)
                {
                    var fieldName = GetFieldName(memberExpr);
                    projection[member.Name] = $"${fieldName}";
                }
            }
        }
        else if (selector.Body is MemberExpression memberExpression)
        {
            // Handle single property access: p.Name
            var fieldName = GetFieldName(memberExpression);
            projection["_value"] = $"${fieldName}";
        }
        else if (selector.Body is UnaryExpression unaryExpression && 
                 unaryExpression.Operand is MemberExpression unaryMemberExpression)
        {
            // Handle nullable property access: p.Price (where Price is nullable)
            var fieldName = GetFieldName(unaryMemberExpression);
            projection["_value"] = $"${fieldName}";
        }
        else
        {
            // For complex expressions, we'll need to handle them case by case
            // For now, throw an exception to indicate unsupported expression
            throw new NotSupportedException($"Unsupported projection expression: {selector.Body.GetType().Name}");
        }
        
        return new BsonDocument("$project", projection);
    }

    /// <summary>
    /// Converts a grouping expression to MongoDB $group stage
    /// </summary>
    public static BsonDocument CreateGroupStage<T, TKey>(Expression<Func<T, TKey>> keySelector) where T : BaseEntity
    {
        var group = new BsonDocument();
        
        if (keySelector.Body is MemberExpression memberExpression)
        {
            var fieldName = GetFieldName(memberExpression);
            group["_id"] = $"${fieldName}";
        }
        else if (keySelector.Body is NewExpression newExpression)
        {
            // Handle composite keys: new { CategoryId = p.CategoryId, Status = p.Status }
            var idFields = new BsonDocument();
            for (int i = 0; i < newExpression.Arguments.Count; i++)
            {
                var argument = newExpression.Arguments[i];
                var member = newExpression.Members?[i];
                
                if (member != null && argument is MemberExpression memberExpr)
                {
                    var fieldName = GetFieldName(memberExpr);
                    idFields[member.Name] = $"${fieldName}";
                }
            }
            group["_id"] = idFields;
        }
        else
        {
            throw new NotSupportedException($"Unsupported grouping expression: {keySelector.Body.GetType().Name}");
        }
        
        // Add items array to collect all documents in each group
        group["items"] = new BsonDocument("$push", "$$ROOT");
        
        return new BsonDocument("$group", group);
    }

    /// <summary>
    /// Converts a field selector to MongoDB field name for aggregation
    /// </summary>
    public static string GetFieldNameForAggregation<T, TValue>(Expression<Func<T, TValue>> fieldSelector) where T : BaseEntity
    {
        if (fieldSelector.Body is MemberExpression memberExpression)
        {
            return GetFieldName(memberExpression);
        }
        else if (fieldSelector.Body is UnaryExpression unaryExpression && 
                 unaryExpression.Operand is MemberExpression unaryMemberExpression)
        {
            return GetFieldName(unaryMemberExpression);
        }
        else
        {
            throw new NotSupportedException($"Unsupported field selector: {fieldSelector.Body.GetType().Name}");
        }
    }

    /// <summary>
    /// Gets the MongoDB field name from a member expression, respecting attribute mappings
    /// </summary>
    private static string GetFieldName(MemberExpression memberExpression)
    {
        var property = memberExpression.Member as PropertyInfo;
        if (property == null)
            throw new ArgumentException("Expression must reference a property");

        // Check for DbId attribute first
        var idAttr = property.GetCustomAttribute<DbIdAttribute>();
        if (idAttr != null)
        {
            return "_id";
        }

        // Check for DbField attribute
        var fieldAttr = property.GetCustomAttribute<DbFieldAttribute>();
        if (fieldAttr != null && !string.IsNullOrEmpty(fieldAttr.FieldName))
        {
            return fieldAttr.FieldName;
        }

        // Check for DbFieldIgnore attribute
        var ignoreAttr = property.GetCustomAttribute<DbFieldIgnore>();
        if (ignoreAttr != null)
        {
            throw new InvalidOperationException($"Property {property.Name} is marked with [DbFieldIgnore] and cannot be used in aggregation");
        }

        // Get the declaring type to check for naming convention
        var declaringType = property.DeclaringType;
        if (declaringType != null)
        {
            var collectionAttr = declaringType.GetCustomAttribute<DbCollectionAttribute>();
            if (collectionAttr != null)
            {
                return ConvertToNamingConvention(property.Name, collectionAttr.NamingConvention);
            }
        }

        // Default to snake_case conversion
        return ConvertToSnakeCase(property.Name);
    }

    /// <summary>
    /// Converts a string to the specified naming convention
    /// </summary>
    private static string ConvertToNamingConvention(string input, DbNamingConvention convention)
    {
        return convention switch
        {
            DbNamingConvention.SnakeCase => ConvertToSnakeCase(input),
            DbNamingConvention.CamelCase => ConvertToCamelCase(input),
            DbNamingConvention.PascalCase => input,
            _ => ConvertToSnakeCase(input)
        };
    }

    /// <summary>
    /// Converts PascalCase to snake_case
    /// </summary>
    private static string ConvertToSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < pascalCase.Length; i++)
        {
            char currentChar = pascalCase[i];
            if (char.IsUpper(currentChar) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(currentChar));
        }
        return result.ToString();
    }

    /// <summary>
    /// Converts PascalCase to camelCase
    /// </summary>
    private static string ConvertToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        if (pascalCase.Length == 1)
            return pascalCase.ToLowerInvariant();

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }

    /// <summary>
    /// Creates a $match stage from a filter expression
    /// </summary>
    public static BsonDocument CreateMatchStage<T>(Expression<Func<T, bool>> filter) where T : BaseEntity
    {
        // Convert the expression to a MongoDB filter
        var mongoFilter = Builders<T>.Filter.Where(filter);
        var filterDoc = mongoFilter.Render(
            BsonSerializer.SerializerRegistry.GetSerializer<T>(),
            BsonSerializer.SerializerRegistry);
        
        return new BsonDocument("$match", filterDoc);
    }

    /// <summary>
    /// Creates a $sort stage from sort expressions
    /// </summary>
    public static BsonDocument CreateSortStage<T>(List<(Expression<Func<T, object>>, bool)> sortExpressions) where T : BaseEntity
    {
        var sort = new BsonDocument();
        
        foreach (var (expression, ascending) in sortExpressions)
        {
            var fieldName = GetFieldNameForAggregation(expression);
            sort[fieldName] = ascending ? 1 : -1;
        }
        
        return new BsonDocument("$sort", sort);
    }

    /// <summary>
    /// Creates aggregation stages for limit and skip
    /// </summary>
    public static List<BsonDocument> CreateLimitSkipStages(int? limit, int? skip)
    {
        var stages = new List<BsonDocument>();
        
        if (skip.HasValue && skip.Value > 0)
        {
            stages.Add(new BsonDocument("$skip", skip.Value));
        }
        
        if (limit.HasValue && limit.Value > 0)
        {
            stages.Add(new BsonDocument("$limit", limit.Value));
        }
        
        return stages;
    }
}
