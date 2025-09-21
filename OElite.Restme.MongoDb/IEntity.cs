using System.Collections.Generic;

namespace OElite.Restme.MongoDb;

/// <summary>
/// Base interface for entities that can be used with MongoDB operations
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    DbObjectId Id { get; set; }
    
    /// <summary>
    /// Status of the entity
    /// </summary>
    EntityStatus Status { get; set; }
    
    /// <summary>
    /// Additional metadata for the entity
    /// </summary>
    Dictionary<string, object>? MetaData { get; set; }
}

/// <summary>
/// Entity status enumeration
/// </summary>
public enum EntityStatus
{
    Active = 1,
    Inactive = 0,
    Deleted = -1
}
