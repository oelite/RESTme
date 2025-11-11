using System.Collections.Generic;

namespace OElite;

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
    /// Additional metadata for the entity
    /// </summary>
    Dictionary<string, object>? MetaData { get; set; }
}