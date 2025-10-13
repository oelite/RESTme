namespace OElite
{
    public enum RestMode
    {
        Http = 0,
        HttpRest = 1,
        LocalFileSystemAsStorage = 5,
        AzureAsStorage = 10,
        AzureAsCache = 11,
        MemoryAsCache = 12,
        RedisAsCache = 20,
        S3AsStorage = 30,
        S3AsCache = 31,
        RabbitMq = 40,
        InMemoryQueue = 41
    }
}