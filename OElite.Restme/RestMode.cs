namespace OElite
{
    public enum RestMode
    {
        HTTPClient = 0,
        HTTPRestClient = 1,
        AzureStorageClient = 10,
        RedisCacheClient = 20,
        S3Client = 30,
        RabbitMq = 40
    }
}