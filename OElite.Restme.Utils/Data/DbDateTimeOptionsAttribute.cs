using System;

namespace OElite;

/// <summary>
/// Specifies options for DateTime serialization in Restme database operations
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DbDateTimeOptionsAttribute : Attribute
{
    /// <summary>
    /// The DateTime kind to use for serialization
    /// </summary>
    public DateTimeKind Kind { get; set; } = DateTimeKind.Utc;

    /// <summary>
    /// Whether to represent the DateTime as a string
    /// </summary>
    public bool Representation { get; set; } = false;

    /// <summary>
    /// The date only representation
    /// </summary>
    public bool DateOnly { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the RestmeDbDateTimeOptionsAttribute
    /// </summary>
    public DbDateTimeOptionsAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the RestmeDbDateTimeOptionsAttribute with specified kind
    /// </summary>
    /// <param name="kind">The DateTime kind</param>
    public DbDateTimeOptionsAttribute(DateTimeKind kind)
    {
        Kind = kind;
    }
}
