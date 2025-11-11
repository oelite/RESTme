using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OElite;

/// <summary>
/// Business exception for OElite platform
/// Used to return user-friendly error messages with error codes
/// </summary>
/// <remarks>
/// This exception is designed to be lightweight and performant.
/// No synchronous logging or HTTP calls - all logging handled by middleware/filters.
/// </remarks>
[Serializable]
public class OEliteException : Exception
{
    /// <summary>
    /// Machine-readable error code for client-side handling
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional contextual details for debugging (optional)
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// HTTP status code to use for this exception (default: 400 Bad Request)
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Create a business exception with a user-friendly message
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="errorCode">Machine-readable error code (default: BUSINESS_ERROR)</param>
    /// <param name="details">Optional additional context</param>
    /// <param name="statusCode">HTTP status code (default: 400)</param>
    public OEliteException(
        string message,
        string errorCode = "BUSINESS_ERROR",
        object? details = null,
        int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Create a business exception with an inner exception
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="innerException">The exception that caused this exception</param>
    /// <param name="errorCode">Machine-readable error code (default: BUSINESS_ERROR)</param>
    /// <param name="details">Optional additional context</param>
    /// <param name="statusCode">HTTP status code (default: 400)</param>
    public OEliteException(
        string message,
        Exception innerException,
        string errorCode = "BUSINESS_ERROR",
        object? details = null,
        int statusCode = 400)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Details = details;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Serialization constructor for exception serialization
    /// </summary>
    protected OEliteException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ErrorCode = info.GetString(nameof(ErrorCode)) ?? "BUSINESS_ERROR";
        Details = info.GetValue(nameof(Details), typeof(object));
        StatusCode = info.GetInt32(nameof(StatusCode));
    }

    /// <summary>
    /// Populate serialization info
    /// </summary>
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ErrorCode), ErrorCode);
        info.AddValue(nameof(Details), Details);
        info.AddValue(nameof(StatusCode), StatusCode);
    }
}

/// <summary>
/// Validation exception for input validation failures
/// </summary>
[Serializable]
public class OEliteValidationException : OEliteException
{
    /// <summary>
    /// Dictionary of field-level validation errors
    /// </summary>
    public Dictionary<string, string> ValidationErrors { get; }

    /// <summary>
    /// Create a validation exception
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="validationErrors">Field-level validation errors</param>
    public OEliteValidationException(
        string message,
        Dictionary<string, string> validationErrors)
        : base(message, "VALIDATION_ERROR", validationErrors, 400)
    {
        ValidationErrors = validationErrors;
    }

    protected OEliteValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ValidationErrors =
            (Dictionary<string, string>)(info.GetValue(nameof(ValidationErrors), typeof(Dictionary<string, string>)) ??
                                         new Dictionary<string, string>());
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ValidationErrors), ValidationErrors);
    }
}

/// <summary>
/// Authorization exception for permission/access denials
/// </summary>
[Serializable]
public class OEliteAuthorizationException : OEliteException
{
    /// <summary>
    /// Required permission that was missing
    /// </summary>
    public string? RequiredPermission { get; }

    /// <summary>
    /// Create an authorization exception
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="requiredPermission">The permission that was required</param>
    public OEliteAuthorizationException(
        string message,
        string? requiredPermission = null)
        : base(message, "AUTHORIZATION_ERROR", requiredPermission, 403)
    {
        RequiredPermission = requiredPermission;
    }

    protected OEliteAuthorizationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        RequiredPermission = info.GetString(nameof(RequiredPermission));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(RequiredPermission), RequiredPermission);
    }
}

/// <summary>
/// Security exception for general security issues
/// </summary>
[Serializable]
public class OEliteSecurityException : OEliteException
{
    /// <summary>
    /// Create an security exception
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="securityConcern">The permission that was required</param>
    public OEliteSecurityException(
        string message,
        string? securityConcern = null)
        : base(message, "AUTHORIZATION_ERROR", securityConcern, 403)
    {
    }

    protected OEliteSecurityException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

/// <summary>
/// Resource not found exception
/// </summary>
[Serializable]
public class OEliteNotFoundException : OEliteException
{
    /// <summary>
    /// Type of resource that was not found
    /// </summary>
    public string? ResourceType { get; }

    /// <summary>
    /// ID of the resource that was not found
    /// </summary>
    public string? ResourceId { get; }

    /// <summary>
    /// Create a not found exception
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="resourceType">Type of resource (e.g., "Product", "Order")</param>
    /// <param name="resourceId">ID of the missing resource</param>
    public OEliteNotFoundException(
        string message,
        string? resourceType = null,
        string? resourceId = null)
        : base(message, "NOT_FOUND", new { ResourceType = resourceType, ResourceId = resourceId }, 404)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    protected OEliteNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ResourceType = info.GetString(nameof(ResourceType));
        ResourceId = info.GetString(nameof(ResourceId));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ResourceType), ResourceType);
        info.AddValue(nameof(ResourceId), ResourceId);
    }
}