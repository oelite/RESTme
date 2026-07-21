using System;
using System.Linq;
using System.Text;

namespace OElite;

/// <summary>
/// Custom ObjectId equivalent for OElite.Common - maps to MongoDB ObjectId in data layer
/// This provides the same functionality as MongoDB ObjectId without requiring MongoDB driver reference
/// </summary>
[Serializable]
public struct DbObjectId : IComparable<DbObjectId>, IEquatable<DbObjectId>
{
    private readonly byte[]? _bytes;
    private readonly string? _stringValue;

    /// <summary>
    /// Creates an DbObjectId from a string representation
    /// </summary>
    /// <param name="value">24-character hex string</param>
    public DbObjectId(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));

        if (value.Length != 24)
            throw new ArgumentException("ObjectId string must be 24 characters long", nameof(value));

        _stringValue = value;
        _bytes = FromHexString(value);
    }

    /// <summary>
    /// Creates an DbObjectId from a byte array
    /// </summary>
    /// <param name="bytes">12-byte array</param>
    public DbObjectId(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        if (bytes.Length != 12)
            throw new ArgumentException("ObjectId must be 12 bytes long", nameof(bytes));

        _bytes = new byte[12];
        Array.Copy(bytes, _bytes, 12);
        _stringValue = ToHexString(_bytes);
    }

    /// <summary>
    /// Gets the string representation of this ObjectId
    /// </summary>
    public string Value => _stringValue ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether this ObjectId is empty/default
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(_stringValue);

    /// <summary>
    /// Creates an empty RestmeObjectId
    /// </summary>
    public static DbObjectId Empty => default(DbObjectId);

    /// <summary>
    /// Gets the byte array representation of this ObjectId
    /// </summary>
    public byte[] ToByteArray()
    {
        if (_bytes == null)
            return new byte[12];

        var result = new byte[12];
        Array.Copy(_bytes, result, 12);
        return result;
    }

    /// <summary>
    /// Returns the string representation of this ObjectId
    /// </summary>
    public override string ToString()
    {
        return _stringValue ?? string.Empty;
    }

    /// <summary>
    /// Returns the hash code for this ObjectId
    /// </summary>
    public override int GetHashCode()
    {
        return _stringValue?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Determines whether the specified object is equal to this ObjectId
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is DbObjectId other)
            return Equals(other);

        if (obj is string str)
            return _stringValue == str;

        return false;
    }

    /// <summary>
    /// Determines whether the specified ObjectId is equal to this ObjectId
    /// </summary>
    public bool Equals(DbObjectId other)
    {
        return _stringValue == other._stringValue;
    }

    /// <summary>
    /// Compares this ObjectId to another ObjectId
    /// </summary>
    public int CompareTo(DbObjectId other)
    {
        return string.Compare(_stringValue ?? string.Empty, other._stringValue ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicit conversion from string to DbObjectId
    /// </summary>
    public static implicit operator DbObjectId(string value)
    {
        return new DbObjectId(value);
    }

    /// <summary>
    /// Implicit conversion from DbObjectId to string
    /// </summary>
    public static implicit operator string(DbObjectId objectId)
    {
        return objectId._stringValue;
    }

    /// <summary>
    /// Equality operator
    /// </summary>
    public static bool operator ==(DbObjectId left, DbObjectId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator
    /// </summary>
    public static bool operator !=(DbObjectId left, DbObjectId right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Less than operator
    /// </summary>
    public static bool operator <(DbObjectId left, DbObjectId right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Less than or equal operator
    /// </summary>
    public static bool operator <=(DbObjectId left, DbObjectId right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Greater than operator
    /// </summary>
    public static bool operator >(DbObjectId left, DbObjectId right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Greater than or equal operator
    /// </summary>
    public static bool operator >=(DbObjectId left, DbObjectId right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Generates a new unique ObjectId
    /// </summary>
    private static byte[] GenerateNewId()
    {
        var bytes = new byte[12];
        var random = new Random();

        // Timestamp (4 bytes) - seconds since Unix epoch
        var timestamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        bytes[0] = (byte)(timestamp >> 24);
        bytes[1] = (byte)(timestamp >> 16);
        bytes[2] = (byte)(timestamp >> 8);
        bytes[3] = (byte)timestamp;

        // Machine identifier (3 bytes) - simplified
        var machineId = Environment.MachineName.GetHashCode();
        bytes[4] = (byte)(machineId >> 16);
        bytes[5] = (byte)(machineId >> 8);
        bytes[6] = (byte)machineId;

        // Process identifier (2 bytes) - simplified
        var processId = Environment.ProcessId;
        bytes[7] = (byte)(processId >> 8);
        bytes[8] = (byte)processId;

        // Counter (3 bytes) - random for simplicity
        var counterBytes = new byte[3];
        random.NextBytes(counterBytes);
        Array.Copy(counterBytes, 0, bytes, 9, 3);

        return bytes;
    }

    /// <summary>
    /// Converts a hex string to byte array
    /// </summary>
    private static byte[] FromHexString(string hex)
    {
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    /// <summary>
    /// Converts a byte array to hex string
    /// </summary>
    private static string ToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a new DbObjectId
    /// </summary>
    public static DbObjectId NewId()
    {
        return new DbObjectId(GenerateNewId());
    }

    /// <summary>
    /// Tries to parse a string as an DbObjectId
    /// </summary>
    public static bool TryParse(string value, out DbObjectId objectId)
    {
        objectId = default;

        if (string.IsNullOrEmpty(value) || value.Length != 24)
            return false;

        try
        {
            // Validate hex characters
            if (value.Any(t => !IsHexChar(t)))
            {
                return false;
            }

            objectId = new DbObjectId(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a character is a valid hex character
    /// </summary>
    private static bool IsHexChar(char c)
    {
        return c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}

public static class DbObjectIdExtensions
{
    /// <summary>
    /// Checks if a DbObjectId is not null or empty.
    /// Returns true if the DbObjectId has a valid value, false if it's null or empty.
    /// </summary>
    /// <param name="value">The DbObjectId value to check</param>
    /// <returns>True if the DbObjectId is not null or empty, false otherwise</returns>
    public static bool IsNotNullOrEmpty(this DbObjectId value)
    {
        return !value.IsEmpty;
    }

    /// <summary>
    /// Checks if a nullable DbObjectId is not null or empty.
    /// Returns true if the DbObjectId has a valid value, false if it's null or empty.
    /// </summary>
    /// <param name="value">The nullable DbObjectId value to check</param>
    /// <returns>True if the DbObjectId is not null or empty, false otherwise</returns>
    public static bool IsNotNullOrEmpty(this DbObjectId? value)
    {
        return value is { IsEmpty: false };
    }

    /// <summary>
    /// Checks if a DbObjectId is null or empty.
    /// Returns true if the DbObjectId is null or empty, false if it has a valid value.
    /// </summary>
    /// <param name="value">The DbObjectId value to check</param>
    /// <returns>True if the DbObjectId is null or empty, false otherwise</returns>
    public static bool IsNullOrEmpty(this DbObjectId value)
    {
        return value.IsEmpty;
    }

    /// <summary>
    /// Checks if a nullable DbObjectId is null or empty.
    /// Returns true if the DbObjectId is null or empty, false if it has a valid value.
    /// </summary>
    /// <param name="value">The nullable DbObjectId value to check</param>
    /// <returns>True if the DbObjectId is null or empty, false otherwise</returns>
    public static bool IsNullOrEmpty(this DbObjectId? value)
    {
        return value is null or { IsEmpty: true };
    }
}