using System.Reflection;
using MongoDB.Bson.Serialization;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Serializes a Status-shadowing entity with a private class-map hierarchy. The global
/// BaseEntity map retains Status for ordinary entities, while this serializer's private
/// BaseEntity map excludes it. Neither registration order nor the frozen global map can
/// change the BSON shape of either entity family.
/// </summary>
public sealed class ShadowedStatusSerializer<T> : IBsonSerializer<T>, IBsonDocumentSerializer, IBsonIdProvider
    where T : BaseEntity
{
    private readonly BsonClassMapSerializer<T> _serializer;

    public ShadowedStatusSerializer()
    {
        var hierarchy = new Stack<Type>();
        for (var type = typeof(T); type != typeof(BaseEntity); type = type.BaseType!)
        {
            hierarchy.Push(type);
        }

        var convention = new RestmeDbAttributeConvention();
        var baseMap = new BsonClassMap(typeof(BaseEntity));
        baseMap.AutoMap();
        convention.Apply(baseMap);
        baseMap.UnmapProperty(nameof(BaseEntity.Status));
        baseMap.Freeze();

        while (hierarchy.Count > 0)
        {
            var type = hierarchy.Pop();
            var map = new BsonClassMap(type, baseMap);
            map.AutoMap();
            convention.Apply(map);

            // Name the declared shadow Status element: honor [DbField] when present,
            // otherwise fall back to the <type>_status prefixed name that
            // MongoPropertyConflictResolver historically used (test-suite contract).
            var statusMap = map.GetMemberMap(nameof(BaseEntity.Status));
            if (statusMap != null)
            {
                var declaredStatus = type.GetProperty(nameof(BaseEntity.Status),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
                var dbField = declaredStatus.GetCustomAttribute<OElite.DbFieldAttribute>();
                statusMap.SetElementName(!string.IsNullOrEmpty(dbField?.FieldName)
                    ? dbField.FieldName!
                    : $"{type.Name.ToLowerInvariant()}_status");
            }

            // These maps are private: never register them in the process-global registry.
            // A shadow declared on an intermediate ancestor takes precedence over its
            // inherited Status, just as the leaf's shadow takes precedence over BaseEntity.
            map.Freeze();
            baseMap = map;
        }

        _serializer = new BsonClassMapSerializer<T>(baseMap);
    }

    public Type ValueType => typeof(T);

    public T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
        _serializer.Deserialize(context, args);

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value) =>
        _serializer.Serialize(context, args, value);

    object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
        Deserialize(context, args)!;

    void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value) =>
        Serialize(context, args, (T)value);

    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo memberSerializationInfo) =>
        _serializer.TryGetMemberSerializationInfo(memberName, out memberSerializationInfo);

    public bool GetDocumentId(object document, out object id, out Type idNominalType, out IIdGenerator idGenerator) =>
        _serializer.GetDocumentId(document, out id, out idNominalType, out idGenerator);

    public void SetDocumentId(object document, object id) => _serializer.SetDocumentId(document, id);
}
