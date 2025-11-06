using System.Collections;
using System.Collections.Generic;

namespace OElite;

public interface IEntityCollection : IEnumerable
{
    int TotalRecordsCount { get; set; }
    Dictionary<string, object>? MetaData { get; set; }
    string BaseEntityTypeName { get; }
}