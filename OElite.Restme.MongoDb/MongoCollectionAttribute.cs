namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB collection attribute - equivalent to RestmeTable
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MongoCollectionAttribute : Attribute
{
    public string CollectionName { get; }
    public string? DefaultSort { get; set; }
    public bool IsView { get; set; } = false;
    public string? ViewPipeline { get; set; }

    public MongoCollectionAttribute(string collectionName)
    {
        CollectionName = collectionName;
    }
}
