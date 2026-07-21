namespace OElite.Restme
{
public enum RestMode
{
    Http = 0,
    HttpRest = 1,
    LocalFileSystem = 5,  // Consolidated from LocalFileSystemAsStorage
    Azure = 10,            // Consolidated from AzureAsStorage + AzureAsCache
    Memory = 12,           // Consolidated from MemoryAsCache + InMemoryQueue
    Redis = 20,            // Consolidated from RedisAsCache
    S3 = 30,               // Consolidated from S3AsStorage + S3AsCache
    RabbitMq = 40,
    ClickHouse = 50,
    Kafka = 60,
    OpenSearch = 70
}
}