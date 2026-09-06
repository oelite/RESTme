using System;

namespace OElite;

/// <summary>
/// Suppresses serialization of the inherited <c>BaseEntity.Status</c> property for the
/// class to which this attribute is applied. Use on a derived entity that shadows
/// <c>BaseEntity.Status</c> with a domain-specific enum (e.g. <c>public new XxxStatus Status</c>).
///
/// Without this attribute, the inherited <c>EntityStatus</c> field is registered in the
/// BSON class map and serialized alongside the shadow property under a different BSON
/// element name. The result is a phantom <c>status</c> field that breaks LINQ queries
/// using the shadow enum.
///
/// Place on the derived class:
/// <code>
/// [BsonSuppressInheritedStatus]
/// public class Subscription : BaseEntity
/// {
///     public new SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
/// }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class BsonSuppressInheritedStatusAttribute : Attribute
{
}
