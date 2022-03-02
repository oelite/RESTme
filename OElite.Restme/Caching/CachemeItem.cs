using System;

namespace OElite.Caching;

public class CachemeItem
{
    public DateTime CachedOnUtc { get; set; }
    public string CacheItemInJson { get; set; }
}