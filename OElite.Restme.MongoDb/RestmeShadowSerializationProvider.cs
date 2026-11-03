using System.Collections.Concurrent;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Serialization provider that owns serialization for BaseEntity subclasses which declare
/// their own <c>Status</c> property (i.e. shadow <see cref="BaseEntity.Status"/> with
/// <c>new</c>). All other types resolve <c>null</c> and fall through to the normal
/// class-map serializer registry — non-shadow entities (helios/core platform entities
/// persisting inherited <c>EntityStatus</c>) are untouched by construction.
/// <para/>
/// Registered once from <see cref="MongoClassMapConfigurator.ConfigureClassMappings"/>;
/// provider precedence means a <c>null</c> return lets the default providers handle the type.
/// <para/>
/// IMPORTANT (driver-version constraint, restme#12 note 63578): provider precedence and
/// <see cref="IBsonDocumentSerializer"/> are stable public contract on MongoDB.Driver 3.x,
/// but any future driver MAJOR bump must re-run the note-63578 spike probes before shipping.
/// </summary>
public class RestmeShadowSerializationProvider : IBsonSerializationProvider
{
    private static readonly ConcurrentDictionary<Type, IBsonSerializer?> SerializerCache = new();

    public IBsonSerializer? GetSerializer(Type type)
    {
        if (!typeof(BaseEntity).IsAssignableFrom(type) || type == typeof(BaseEntity))
            return null;

        if (!HasShadowedStatus(type))
            return null;

        return SerializerCache.GetOrAdd(type, static t =>
        {
            var serializerType = typeof(ShadowedStatusSerializer<>).MakeGenericType(t);
            return (IBsonSerializer)Activator.CreateInstance(serializerType)!;
        });
    }

    private static bool HasShadowedStatus(Type type)
    {
        // DeclaredOnly avoids AmbiguousMatchException when multiple Status properties exist
        // in the hierarchy — a shadow is a Status property declared directly on the type.
        return type.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null;
    }
}
