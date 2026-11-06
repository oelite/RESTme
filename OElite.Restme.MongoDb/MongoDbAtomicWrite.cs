using System.Linq.Expressions;
using System.Reflection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace OElite.Restme.MongoDb;

/// <summary>Pure command construction and outcome mapping; no connection or database access.</summary>
internal static class MongoDbAtomicWrite
{
    internal static (BsonDocument Filter, BsonDocument Replacement) PrepareCompareExchange<T>(
        T replacement, Expression<Func<T, long>> version, long expectedVersion,
        Expression<Func<T, bool>>? condition, IBsonSerializer<T> serializer) where T : BaseEntity
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(version);
        if (replacement.Id.IsNullOrEmpty())
        {
            throw new ArgumentException("A persisted document identity is required.", nameof(replacement));
        }
        if (expectedVersion < 0 || expectedVersion == long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }
        if (version.Body is not MemberExpression { Member: PropertyInfo property } member ||
            member.Expression != version.Parameters[0] || property.SetMethod?.IsPublic != true)
        {
            throw new ArgumentException("Select a direct writable long property.", nameof(version));
        }
        if (serializer is not IBsonDocumentSerializer documentSerializer ||
            !documentSerializer.TryGetMemberSerializationInfo(property.Name, out var versionInfo) ||
            versionInfo.ElementName == "_id" || versionInfo.ElementName.Contains('.') ||
            versionInfo.ElementName.StartsWith('$'))
        {
            throw new ArgumentException("The version must be a serialized top-level long property.", nameof(version));
        }

        var snapshot = replacement.ToBsonDocument(serializer);
        if (!snapshot.TryGetValue(versionInfo.ElementName, out var storedVersion) || !storedVersion.IsInt64)
        {
            throw new ArgumentException("The version must serialize as an Int64 field.", nameof(version));
        }
        if (!snapshot.TryGetValue("_id", out var identity) || identity.IsBsonNull)
        {
            throw new ArgumentException("The document must serialize an identity.", nameof(replacement));
        }
        snapshot[versionInfo.ElementName] = expectedVersion + 1;
        var predicates = new BsonArray
        {
            new BsonDocument("_id", identity),
            new BsonDocument(versionInfo.ElementName, expectedVersion)
        };
        if (condition != null)
        {
            var rendered = new ExpressionFilterDefinition<T>(condition).Render(
                new RenderArgs<T>(serializer, BsonSerializer.SerializerRegistry));
            predicates.Add(rendered);
        }
        return (new BsonDocument("$and", predicates), snapshot);
    }

    internal static MongoDbCasResult<T> MapReplaceResult<T>(ReplaceOneResult result, T replacement, long expectedVersion)
        where T : BaseEntity
    {
        if (!result.IsAcknowledged)
        {
            throw new MongoDbWriteException(MongoDbWriteFailure.OutcomeUnknown);
        }
        return result.MatchedCount switch
        {
            0 => new MongoDbCasResult<T>(
                MongoDbCasResultStatus.Conflict, null, null, null, "Conditional write did not match."),
            1 when result.ModifiedCount == 1 => new MongoDbCasResult<T>(
                MongoDbCasResultStatus.Updated, replacement, expectedVersion, expectedVersion + 1, null),
            _ => throw new MongoDbWriteException(MongoDbWriteFailure.OutcomeUnknown)
        };
    }

    internal static bool IsDuplicate(MongoWriteException exception)
        => exception.WriteError?.Category == ServerErrorCategory.DuplicateKey &&
           exception.WriteConcernError == null;

    internal static Expression<Func<T, long>> VersionPropertyExpression<T>(string propertyName)
        where T : BaseEntity
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || property.PropertyType != typeof(long) || property.GetSetMethod()?.IsPublic != true)
        {
            throw new ArgumentException("Select a direct writable long property.", nameof(propertyName));
        }
        var parameter = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, long>>(Expression.Property(parameter, property), parameter);
    }
}
