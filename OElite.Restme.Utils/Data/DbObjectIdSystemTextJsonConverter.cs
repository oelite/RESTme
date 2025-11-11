using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OElite;

/// <summary>
/// System.Text.Json converter for DbObjectId (used by output formatter)
/// </summary>
public class DbObjectIdSystemTextJsonConverter : JsonConverter<DbObjectId>
{
    public override DbObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default(DbObjectId);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();

            if (string.IsNullOrEmpty(stringValue))
            {
                return default(DbObjectId);
            }

            // Try to parse the string as DbObjectId
            if (DbObjectId.TryParse(stringValue, out var objectId))
            {
                return objectId;
            }

            // If parsing fails, return default
            return default(DbObjectId);
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' when parsing DbObjectId. Expected String or Null.");
    }

    public override void Write(Utf8JsonWriter writer, DbObjectId value, JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value);
        }
    }
}

/// <summary>
/// System.Text.Json converter for nullable DbObjectId
/// </summary>
public class NullableDbObjectIdSystemTextJsonConverter : JsonConverter<DbObjectId?>
{
    public override DbObjectId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();

            if (string.IsNullOrEmpty(stringValue))
            {
                return null;
            }

            // Try to parse the string as DbObjectId
            if (DbObjectId.TryParse(stringValue, out var objectId))
            {
                return objectId;
            }

            // If parsing fails, return null
            return null;
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' when parsing nullable DbObjectId. Expected String or Null.");
    }

    public override void Write(Utf8JsonWriter writer, DbObjectId? value, JsonSerializerOptions options)
    {
        if (value == null || value.Value.IsEmpty)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.Value);
        }
    }
}
