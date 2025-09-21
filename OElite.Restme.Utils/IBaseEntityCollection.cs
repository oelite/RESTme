using System.Collections;

namespace OElite;

public interface IBaseEntityCollection : IEnumerable
{
    int TotalRecordsCount { get; set; }
}