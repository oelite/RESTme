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
    /// Possible values: "eu", "us", "apac", "ca", "uk", "cn", etc.
    /// </summary>
    [DbField("region")]
    public string? Region { get; set; }

    [DbFieldIgnore] public Dictionary<string, object>? MetaData { get; set; }
}

public class BaseEntityCollection<T> : List<T>, IBaseEntityCollection
    where T : BaseEntity
{
    public int TotalRecordsCount { get; set; }
    public Dictionary<string, object>? MetaData { get; set; }

    public bool SearchIndexAllowed => true;
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
}