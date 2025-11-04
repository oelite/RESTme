using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace OElite.Restme.MongoDb.Management;

/// <summary>
/// MongoDB implementation of database management provider
/// Provides sharding, indexing, and bootstrap functionality
/// </summary>
public class MongoDbManagementProvider : IDbManagementProvider
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;
    private readonly string _databaseName;

    public MongoDbManagementProvider(IMongoDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _client = database.Client;
        _databaseName = database.DatabaseNamespace.DatabaseName;
    }

    public MongoDbManagementProvider(string connectionString, string databaseName)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        if (string.IsNullOrEmpty(databaseName))
            throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));

        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase(databaseName);
        _databaseName = databaseName;
    }

    public async Task<DbManagementResult> InitializeDatabaseAsync(DbBootstrapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DbManagementResult();
        var messages = new List<string>();

        try
        {
            // Validate configuration first
            var validationResult = await ValidateBootstrapConfigurationAsync(configuration, cancellationToken);
            if (!validationResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = "Configuration validation failed";
                result.Messages = validationResult.Messages;
                return result;
            }

            // Enable sharding if requested
            if (configuration.EnableSharding)
            {
                var shardingResult = await EnableShardingAsync(_databaseName, cancellationToken);
                messages.AddRange(shardingResult.Messages);

                if (!shardingResult.Success)
                {
                    messages.Add($"Warning: Could not enable sharding - {shardingResult.ErrorMessage}");
                }
            }

            // Process each collection configuration
            foreach (var collectionConfig in configuration.Collections)
            {
                await ProcessCollectionConfigurationAsync(collectionConfig, configuration, messages, cancellationToken);
            }

            result.Success = true;
            result.Messages = messages;
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Messages = messages;
            result.ExecutionTime = stopwatch.Elapsed;
            return result;
        }
    }

    public async Task<DbManagementResult> EnableShardingAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DbManagementResult();

        try
        {
            var adminDb = _client.GetDatabase("admin");

            // Check if sharding is available (cluster mode)
            try
            {
                var shardStatus = await adminDb.RunCommandAsync<BsonDocument>(
                    new BsonDocument("listShards", 1), cancellationToken: cancellationToken);

                if (shardStatus["shards"].AsBsonArray.Count == 0)
                {
                    result.Success = true;
                    result.Messages.Add("No shards detected - running in standalone mode, sharding not available");
                    return result;
                }
            }
            catch (MongoCommandException)
            {
                result.Success = true;
                result.Messages.Add("Sharding not available - running in standalone or replica set mode");
                return result;
            }

            // Enable sharding on database
            var enableShardCmd = new BsonDocument("enableSharding", databaseName);
            await adminDb.RunCommandAsync<BsonDocument>(enableShardCmd, cancellationToken: cancellationToken);

            result.Success = true;
            result.Messages.Add($"Sharding enabled for database: {databaseName}");
        }
        catch (MongoCommandException ex) when (ex.CodeName == "AlreadyInitialized")
        {
            result.Success = true;
            result.Messages.Add($"Sharding already enabled for database: {databaseName}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<DbManagementResult> CreateShardedCollectionAsync(string collectionName, DbShardKey shardKey, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DbManagementResult();

        try
        {
            var adminDb = _client.GetDatabase("admin");
            var shardKeyDoc = ConvertShardKeyToBsonDocument(shardKey);

            var shardCmd = new BsonDocument("shardCollection", $"{_databaseName}.{collectionName}")
            {
                ["key"] = shardKeyDoc,
                ["unique"] = shardKey.IsUnique
            };

            await adminDb.RunCommandAsync<BsonDocument>(shardCmd, cancellationToken: cancellationToken);

            result.Success = true;
            result.Messages.Add($"Sharded collection created: {collectionName}");
        }
        catch (MongoCommandException ex) when (ex.CodeName == "AlreadyInitialized")
        {
            result.Success = true;
            result.Messages.Add($"Collection already sharded: {collectionName}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<DbManagementResult> CreateIndexesAsync(string collectionName, List<DbIndexDefinition> indexes, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DbManagementResult();

        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var indexModels = new List<CreateIndexModel<BsonDocument>>();

            foreach (var indexDef in indexes)
            {
                var indexKeys = ConvertIndexFieldsToBsonDocument(indexDef.Fields);
                var options = new CreateIndexOptions
                {
                    Name = indexDef.Name,
                    Unique = indexDef.IsUnique,
                    Sparse = indexDef.IsSparse,
                    Background = indexDef.CreateInBackground
                };

                if (indexDef.TtlExpiration.HasValue)
                {
                    options.ExpireAfter = indexDef.TtlExpiration.Value;
                }

                // Note: PartialFilterExpression support requires specific MongoDB driver version
                // Commented out for compatibility - can be enabled if needed
                // if (indexDef.PartialFilterExpression != null)
                // {
                //     options.PartialFilterExpression = ConvertMongoDbDocumentToBsonDocument(indexDef.PartialFilterExpression);
                // }

                indexModels.Add(new CreateIndexModel<BsonDocument>(indexKeys, options));
            }

            await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);

            result.Success = true;
            result.Messages.Add($"Created {indexes.Count} indexes for collection: {collectionName}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<DbManagementResult> PreSplitShardsAsync(string collectionName, DbShardKey shardKey, int splitCount, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DbManagementResult();
        var successfulSplits = 0;

        try
        {
            var adminDb = _client.GetDatabase("admin");

            // Generate split points based on shard key
            var splitPoints = GenerateSplitPoints(shardKey, splitCount);

            foreach (var splitPoint in splitPoints)
            {
                try
                {
                    var splitCmd = new BsonDocument("split", $"{_databaseName}.{collectionName}")
                    {
                        ["middle"] = splitPoint
                    };

                    await adminDb.RunCommandAsync<BsonDocument>(splitCmd, cancellationToken: cancellationToken);
                    successfulSplits++;
                }
                catch (MongoCommandException ex) when (ex.CodeName == "CannotSplit")
                {
                    // Expected for some split points, continue
                    continue;
                }
            }

            result.Success = true;
            result.Messages.Add($"Pre-split {successfulSplits} chunks for collection: {collectionName}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<DbShardingStatus> GetShardingStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new DbShardingStatus();

        try
        {
            var adminDb = _client.GetDatabase("admin");

            // Get shard list
            var shardStatus = await adminDb.RunCommandAsync<BsonDocument>(
                new BsonDocument("listShards", 1), cancellationToken: cancellationToken);

            status.IsShardingEnabled = true;
            status.ShardCount = shardStatus["shards"].AsBsonArray.Count;

            foreach (var shardDoc in shardStatus["shards"].AsBsonArray)
            {
                var shard = shardDoc.AsBsonDocument;
                status.Shards.Add(new DbShardInfo
                {
                    ShardId = shard["_id"].AsString,
                    Host = shard["host"].AsString,
                    IsActive = shard.GetValue("state", BsonInt32.Create(1)).AsInt32 == 1
                });
            }

            // Get sharded collections
            var configDb = _client.GetDatabase("config");
            var collectionsCollection = configDb.GetCollection<BsonDocument>("collections");
            var shardedCollections = await collectionsCollection
                .Find(new BsonDocument("_id", new BsonRegularExpression($"^{_databaseName}\\.")))
                .ToListAsync(cancellationToken);

            foreach (var collection in shardedCollections)
            {
                var fullName = collection["_id"].AsString;
                var collectionName = fullName.Substring(_databaseName.Length + 1);
                status.ShardedCollections.Add(collectionName);
            }
        }
        catch (MongoCommandException)
        {
            status.IsShardingEnabled = false;
            status.ShardCount = 0;
        }

        return status;
    }

    public async Task<List<DbIndexInfo>> GetIndexesAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var collection = _database.GetCollection<BsonDocument>(collectionName);
        var indexes = new List<DbIndexInfo>();

        try
        {
            var indexCursor = await collection.Indexes.ListAsync(cancellationToken);
            var indexDocuments = await indexCursor.ToListAsync(cancellationToken);

            foreach (var indexDoc in indexDocuments)
            {
                var indexInfo = new DbIndexInfo
                {
                    Name = indexDoc["name"].AsString,
                    KeySpec = ConvertBsonDocumentToDictionary(indexDoc["key"].AsBsonDocument),
                    IsUnique = indexDoc.GetValue("unique", BsonBoolean.False).AsBoolean,
                    IsSparse = indexDoc.GetValue("sparse", BsonBoolean.False).AsBoolean
                };

                if (indexDoc.Contains("expireAfterSeconds"))
                {
                    indexInfo.TtlSeconds = indexDoc["expireAfterSeconds"].AsInt32;
                }

                indexes.Add(indexInfo);
            }
        }
        catch (Exception)
        {
            // Collection might not exist or other issues
        }

        return indexes;
    }

    public async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
            var collectionList = await collections.ToListAsync(cancellationToken);
            return collectionList.Contains(collectionName);
        }
        catch
        {
            return false;
        }
    }

    public async Task<DbStatistics> GetDatabaseStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DbStatistics { DatabaseName = _databaseName };

        try
        {
            var statsCommand = new BsonDocument("dbStats", 1);
            var result = await _database.RunCommandAsync<BsonDocument>(statsCommand, cancellationToken: cancellationToken);

            stats.CollectionCount = result.GetValue("collections", BsonInt64.Create(0)).AsInt64;
            stats.DataSizeInBytes = result.GetValue("dataSize", BsonInt64.Create(0)).AsInt64;
            stats.StorageSizeInBytes = result.GetValue("storageSize", BsonInt64.Create(0)).AsInt64;
            stats.IndexSizeInBytes = result.GetValue("indexSize", BsonInt64.Create(0)).AsInt64;
            stats.SizeInBytes = stats.DataSizeInBytes + stats.IndexSizeInBytes;
            stats.AverageObjectSizeInBytes = result.GetValue("avgObjSize", BsonDouble.Create(0)).AsDouble;
        }
        catch (Exception)
        {
            // Handle cases where stats are not available
        }

        return stats;
    }

    public async Task<DbCollectionStatistics> GetCollectionStatisticsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var stats = new DbCollectionStatistics { CollectionName = collectionName };

        try
        {
            var statsCommand = new BsonDocument("collStats", collectionName);
            var result = await _database.RunCommandAsync<BsonDocument>(statsCommand, cancellationToken: cancellationToken);

            stats.DocumentCount = result.GetValue("count", BsonInt64.Create(0)).AsInt64;
            stats.SizeInBytes = result.GetValue("size", BsonInt64.Create(0)).AsInt64;
            stats.AverageDocumentSizeInBytes = result.GetValue("avgObjSize", BsonDouble.Create(0)).AsDouble;
            stats.IndexCount = result.GetValue("nindexes", BsonInt32.Create(0)).AsInt32;
            stats.TotalIndexSizeInBytes = result.GetValue("totalIndexSize", BsonInt64.Create(0)).AsInt64;
            stats.IsSharded = result.GetValue("sharded", BsonBoolean.False).AsBoolean;

            if (stats.IsSharded && result.Contains("shardKey"))
            {
                stats.ShardKey = ConvertBsonDocumentToDictionary(result["shardKey"].AsBsonDocument);
            }
        }
        catch (Exception)
        {
            // Handle cases where collection doesn't exist or stats are not available
        }

        return stats;
    }

    public async Task<DbManagementResult> ValidateBootstrapConfigurationAsync(DbBootstrapConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var result = new DbManagementResult();
        var messages = new List<string>();

        try
        {
            // Initialize as successful, set to false only when errors occur
            result.Success = true;

            messages.Add($"Starting validation for configuration with {configuration.Collections?.Count ?? 0} collections");

            // Validate basic configuration
            if (configuration.Collections == null || !configuration.Collections.Any())
            {
                messages.Add("Warning: No collections configured for bootstrap");
                result.Success = false;
                result.ErrorMessage = "No collections configured for bootstrap";
                result.Messages = messages;
                return result;
            }

            // Validate each collection configuration
            foreach (var collectionConfig in configuration.Collections)
            {
                messages.Add($"Validating collection: {collectionConfig.CollectionName}");

                if (string.IsNullOrEmpty(collectionConfig.CollectionName))
                {
                    messages.Add("Error: Collection name cannot be empty");
                    result.Success = false;
                }

                // Validate shard key if specified
                if (collectionConfig.ShardKey != null)
                {
                    messages.Add($"Validating shard key for collection '{collectionConfig.CollectionName}' with {collectionConfig.ShardKey.Fields?.Count ?? 0} fields");

                    if (collectionConfig.ShardKey.Fields == null || !collectionConfig.ShardKey.Fields.Any())
                    {
                        messages.Add($"Error: Shard key for collection '{collectionConfig.CollectionName}' has no fields");
                        result.Success = false;
                    }
                    else
                    {
                        foreach (var field in collectionConfig.ShardKey.Fields)
                        {
                            if (string.IsNullOrEmpty(field.FieldName))
                            {
                                messages.Add($"Error: Shard key field name cannot be empty in collection '{collectionConfig.CollectionName}'");
                                result.Success = false;
                            }
                            else
                            {
                                messages.Add($"Shard key field validated: {field.FieldName}");
                            }
                        }
                    }
                }

                // Validate indexes
                messages.Add($"Validating {collectionConfig.Indexes?.Count ?? 0} indexes for collection '{collectionConfig.CollectionName}'");
                if (collectionConfig.Indexes != null)
                {
                    foreach (var index in collectionConfig.Indexes)
                    {
                        messages.Add($"Validating index: {index.Name} with {index.Fields?.Count ?? 0} fields");

                        if (string.IsNullOrEmpty(index.Name))
                        {
                            messages.Add($"Warning: Index in collection '{collectionConfig.CollectionName}' has no name");
                        }

                        if (index.Fields == null || !index.Fields.Any())
                        {
                            messages.Add($"Error: Index '{index.Name}' in collection '{collectionConfig.CollectionName}' has no fields");
                            result.Success = false;
                        }
                        else
                        {
                            foreach (var field in index.Fields)
                            {
                                if (string.IsNullOrEmpty(field.FieldName))
                                {
                                    messages.Add($"Error: Index '{index.Name}' in collection '{collectionConfig.CollectionName}' has empty field name");
                                    result.Success = false;
                                }
                                else
                                {
                                    messages.Add($"Index field validated: {field.FieldName}");
                                }
                            }
                        }
                    }
                }
            }

            if (!result.Success)
            {
                result.ErrorMessage = $"Configuration validation failed. Details: {string.Join("; ", messages.Where(m => m.StartsWith("Error")))}";
            }
            else
            {
                messages.Add("Configuration validation successful");
            }

            result.Messages = messages;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Validation exception: {ex.Message}";
            messages.Add($"Exception during validation: {ex}");
            result.Messages = messages;
        }

        return result;
    }

    #region Private Helper Methods

    private async Task ProcessCollectionConfigurationAsync(
        DbCollectionConfiguration collectionConfig,
        DbBootstrapConfiguration globalConfig,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        // Create indexes first (required before sharding)
        if (collectionConfig.Indexes.Any())
        {
            var indexResult = await CreateIndexesAsync(collectionConfig.CollectionName, collectionConfig.Indexes, cancellationToken);
            messages.AddRange(indexResult.Messages);

            if (!indexResult.Success)
            {
                messages.Add($"Warning: Failed to create indexes for collection {collectionConfig.CollectionName} - {indexResult.ErrorMessage}");
            }
        }

        // Set up sharding if requested
        if (collectionConfig.ShardKey != null && globalConfig.EnableSharding)
        {
            var shardResult = await CreateShardedCollectionAsync(collectionConfig.CollectionName, collectionConfig.ShardKey, cancellationToken);
            messages.AddRange(shardResult.Messages);

            if (!shardResult.Success)
            {
                messages.Add($"Warning: Failed to shard collection {collectionConfig.CollectionName} - {shardResult.ErrorMessage}");
            }
            else if (globalConfig.EnablePreSplitting)
            {
                var splitResult = await PreSplitShardsAsync(collectionConfig.CollectionName, collectionConfig.ShardKey, globalConfig.PreSplitCount, cancellationToken);
                messages.AddRange(splitResult.Messages);
            }
        }
    }

    private static BsonDocument ConvertShardKeyToBsonDocument(DbShardKey shardKey)
    {
        var doc = new BsonDocument();
        foreach (var field in shardKey.Fields)
        {
            if (field.IsHashed)
            {
                doc[field.FieldName] = "hashed";
            }
            else
            {
                doc[field.FieldName] = (int)field.Direction;
            }
        }
        return doc;
    }

    private static BsonDocument ConvertIndexFieldsToBsonDocument(List<DbIndexField> fields)
    {
        var doc = new BsonDocument();
        foreach (var field in fields)
        {
            if (field.IsHashed)
            {
                doc[field.FieldName] = "hashed";
            }
            else if (field.IsText)
            {
                doc[field.FieldName] = "text";
            }
            else
            {
                doc[field.FieldName] = (int)field.Direction;
            }
        }
        return doc;
    }

    private static BsonDocument ConvertMongoDbDocumentToBsonDocument(MongoDbDocument mongoDbDoc)
    {
        var bsonDoc = new BsonDocument();
        foreach (var kvp in mongoDbDoc)
        {
            bsonDoc[kvp.Key] = BsonValue.Create(kvp.Value);
        }
        return bsonDoc;
    }

    private static Dictionary<string, object> ConvertBsonDocumentToDictionary(BsonDocument bsonDoc)
    {
        var dict = new Dictionary<string, object>();
        foreach (var element in bsonDoc.Elements)
        {
            dict[element.Name] = ConvertBsonValueToObject(element.Value);
        }
        return dict;
    }

    private static object ConvertBsonValueToObject(BsonValue bsonValue)
    {
        return bsonValue.BsonType switch
        {
            BsonType.String => bsonValue.AsString,
            BsonType.Int32 => bsonValue.AsInt32,
            BsonType.Int64 => bsonValue.AsInt64,
            BsonType.Double => bsonValue.AsDouble,
            BsonType.Boolean => bsonValue.AsBoolean,
            BsonType.DateTime => bsonValue.ToUniversalTime(),
            _ => bsonValue.ToString()
        };
    }

    private static List<BsonDocument> GenerateSplitPoints(DbShardKey shardKey, int splitCount)
    {
        var splitPoints = new List<BsonDocument>();

        // For simplicity, generate split points based on the first field
        var firstField = shardKey.Fields.FirstOrDefault();
        if (firstField == null) return splitPoints;

        if (firstField.IsHashed)
        {
            // For hashed shard keys, generate hex-based split points
            for (int i = 1; i < splitCount; i++)
            {
                var splitPoint = new BsonDocument();
                var hexValue = (i * (256 / splitCount)).ToString("x2").PadLeft(2, '0');
                splitPoint[firstField.FieldName] = hexValue;
                splitPoints.Add(splitPoint);
            }
        }
        else
        {
            // For non-hashed keys, generate string-based split points
            for (int i = 1; i < splitCount; i++)
            {
                var splitPoint = new BsonDocument();
                splitPoint[firstField.FieldName] = $"split_{i:D4}";
                splitPoints.Add(splitPoint);
            }
        }

        return splitPoints;
    }

    #endregion
}