using OElite.Restme.MongoDb;

namespace OElite.Restme.MongoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Concrete implementation of MongoDbCentre for testing
/// </summary>
public class TestMongoDbCentre : MongoDbCentre
{
    public TestMongoDbCentre(string mongoDbConnectionString) : base(mongoDbConnectionString)
    {
    }
}