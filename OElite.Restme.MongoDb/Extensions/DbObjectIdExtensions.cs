namespace OElite.Restme.MongoDb.Extensions;

/// <summary>
/// Extension methods for string IDs to support MongoDB operations
/// </summary>
public static class ObjectIdExtensions
{
    /// <summary>
    /// Validates if string is a valid ObjectId format
    /// </summary>
    public static bool IsValidObjectId(this string objectIdString)
    {
        if (string.IsNullOrEmpty(objectIdString))
        {
            return false;
        }

        // ObjectId is 24 character hex string
        if (objectIdString.Length != 24)
        {
            return false;
        }

        // Check if all characters are valid hex
        return objectIdString.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    /// <summary>
    /// Validates ObjectId format and throws if invalid
    /// </summary>
    public static string ValidateObjectId(this string objectIdString)
    {
        if (string.IsNullOrEmpty(objectIdString))
        {
            throw new ArgumentException("Object ID string cannot be null or empty", nameof(objectIdString));
        }

        if (!objectIdString.IsValidObjectId())
        {
            throw new FormatException($"Invalid ObjectId format: {objectIdString}");
        }

        return objectIdString;
    }
}