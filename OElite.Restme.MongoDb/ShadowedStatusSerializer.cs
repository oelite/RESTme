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

    /// <summary>
    /// Issue #22 (#1564 Cat 3 alias collision): shadow member name → alias member name.
    /// When an alias property claims the element name the shadow would use, the shadow's
    /// member map is unmapped and queries on the shadow member are routed through the
    /// alias's serialization info (the alias's computed accessor keeps the value in sync).
    /// </summary>
    private readonly Dictionary<string, string> _aliasMemberNames = new(StringComparer.Ordinal);

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
                var shadowElementName = !string.IsNullOrEmpty(dbField?.FieldName)
                    ? dbField.FieldName!
                    : $"{type.Name.ToLowerInvariant()}_status";

                // Issue #22 (#1564 Cat 3 alias collision, regression shipped in
                // 2.1.1-develop.15334): when ANOTHER declared property on this level
                // resolves to the SAME element name (a computed alias like
                // [DbField("device_status")] DeviceState { get => Status; set => Status = value; }),
                // renaming the shadow to that name makes Freeze() throw a duplicate-element
                // BsonSerializationException. The ALIAS owns the shared element ("alias
                // wins", pre-15334 behavior): the shadow's member map is unmapped so the
                // document stores exactly ONE status-named element, and the shadow
                // round-trips through the alias's computed accessor. When no alias exists,
                // the explicit [DbField]/fallback rename below is preserved unchanged
                // (all issue #12 acceptance criteria hold).
                var aliasMap = map.DeclaredMemberMaps.FirstOrDefault(mm =>
                    !ReferenceEquals(mm, statusMap) && mm.ElementName == shadowElementName);

                if (aliasMap != null)
                {
                    map.UnmapProperty(nameof(BaseEntity.Status));
                    _aliasMemberNames[nameof(BaseEntity.Status)] = aliasMap.MemberName;
                }
                else
                {
                    statusMap.SetElementName(shadowElementName);
                }
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

    public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo memberSerializationInfo)
    {
        // Issue #22: when the shadow Status member was unmapped in favor of an alias
        // (alias-wins), route member resolution for the shadow through the alias's
        // serialization info so LINQ3 filters/updates on the shadow still bind to the
        // single shared element.
        if (_serializer.TryGetMemberSerializationInfo(memberName, out memberSerializationInfo))
        {
            return true;
        }

        if (_aliasMemberNames.TryGetValue(memberName, out var aliasMemberName) &&
            _serializer.TryGetMemberSerializationInfo(aliasMemberName, out memberSerializationInfo))
        {
            return true;
        }

        return false;
    }

    public bool GetDocumentId(object document, out object id, out Type idNominalType, out IIdGenerator idGenerator) =>
        _serializer.GetDocumentId(document, out id, out idNominalType, out idGenerator);

    public void SetDocumentId(object document, object id) => _serializer.SetDocumentId(document, id);
}
