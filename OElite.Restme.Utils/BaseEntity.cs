using System.Collections.Generic;
using System.Linq;

namespace OElite;

public class BaseEntity : IEntity
{
    [DbId(DbIdType.DbObjectId)]
    [DbField("_id")]
    public DbObjectId Id { get; set; }

    public EntityStatus Status { get; set; }

    /// <summary>
    /// Geographic region for GDPR compliance and data sovereignty
    /// Used for region-aware sharding and data placement
    /// Possible values: "gb", "us", "cn", etc. using countries' ISO 3166-1 alpha-2 codes
    /// </summary>
    [DbField("region")]
    public string? Region { get; set; }

    [DbFieldIgnore] public Dictionary<string, object>? MetaData { get; set; }
}

public class BaseEntityCollection<T> : List<T>, IEntityCollection
    where T : BaseEntity
{
    public int TotalRecordsCount { get; set; }
    public Dictionary<string, object>? MetaData { get; set; }

    public string BaseEntityTypeName => typeof(T).Name;
}

public class DataCollection<T> : List<T>, IEntityCollection where T : class
{
    public int TotalRecordsCount { get; set; }
    public Dictionary<string, object>? MetaData { get; set; }
    public string BaseEntityTypeName => typeof(T).Name;
}

public static class BaseEntityCollectionHelpers
{
    public static TC? ToBaseEntityCollection<T, TC>(this IEnumerable<T> data,
        int totalRecordCount,
        bool returnEmptyCollectionIfNullOrNoRecords = true)
        where TC : BaseEntityCollection<T>, new() where T : BaseEntity
    {
        var result = new TC();
        var items = data?.ToList();
        if (!(items?.Count > 0)) return returnEmptyCollectionIfNullOrNoRecords ? result : null;

        result.AddRange(items);
        if (totalRecordCount > 0)
        {
            result.TotalRecordsCount = totalRecordCount;
        }

        return result;
    }

    public static TC? ToDataCollection<T, TC>(this IEnumerable<T> data,
        int totalRecordCount,
        bool returnEmptyCollectionIfNullOrNoRecords = true)
        where TC : DataCollection<T>, new() where T : class
    {
        var result = new TC();
        var items = data?.ToList();
        if (!(items?.Count > 0)) return returnEmptyCollectionIfNullOrNoRecords ? result : null;

        result.AddRange(items);
        if (totalRecordCount > 0)
        {
            result.TotalRecordsCount = totalRecordCount;
        }

        return result;
    }

    /// <summary>
    /// Sets the total records count for the collection
    /// </summary>
    public static TC SetTotalRecordsCount<T, TC>(this TC collection, int totalRecordsCount)
        where TC : IEntityCollection where T : class
    {
        collection.TotalRecordsCount = totalRecordsCount;
        return collection;
    }

    /// <summary>
    /// Adds entities to the collection
    /// </summary>
    public static TC AddEntities<T, TC>(this TC collection, IEnumerable<T> entities)
        where TC : BaseEntityCollection<T> where T : BaseEntity
    {
        if (entities != null)
        {
            collection.AddRange(entities);
        }

        return collection;
    }

    public static TC AddData<T, TC>(this TC collection, IEnumerable<T> entities)
        where TC : DataCollection<T> where T : class
    {
        if (entities != null)
        {
            collection.AddRange(entities);
        }

        return collection;
    }

    /// <summary>
    /// Adds metadata to the collection
    /// </summary>
    public static TC AddMetaData<T, TC>(this TC collection, string key, object value)
        where TC : IEntityCollection where T : class
    {
        collection.MetaData ??= new Dictionary<string, object>();
        collection.MetaData[key] = value;
        return collection;
    }

    /// <summary>
    /// Adds multiple metadata entries to the collection
    /// </summary>
    public static TC AddMetaData<T, TC>(this TC collection, Dictionary<string, object> metadata)
        where TC : IEntityCollection where T : class
    {
        if (metadata == null) return collection;

        collection.MetaData ??= new Dictionary<string, object>();
        foreach (var kvp in metadata)
        {
            collection.MetaData[kvp.Key] = kvp.Value;
        }

        return collection;
    }
}