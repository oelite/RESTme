using Newtonsoft.Json;
using System;

namespace OElite;

/// <summary>
/// Custom JSON converter for DbObjectId that handles deserialization from string values
/// </summary>
public class DbObjectIdJsonConverter : JsonConverter<DbObjectId>
{
    public override DbObjectId ReadJson(JsonReader reader, Type objectType, DbObjectId existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return default(DbObjectId);
        }

        if (reader.TokenType == JsonToken.String)
        {
            var stringValue = reader.Value?.ToString();

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

        throw new JsonSerializationException($"Unexpected token type '{reader.TokenType}' when parsing DbObjectId. Expected String or Null.");
    }

    public override void WriteJson(JsonWriter writer, DbObjectId value, JsonSerializer serializer)
    {
        if (value.IsEmpty)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value.Value);
        }
    }
}

/// <summary>
/// Custom JSON converter for nullable DbObjectId
/// </summary>
public class NullableDbObjectIdJsonConverter : JsonConverter<DbObjectId?>
{
    public override DbObjectId? ReadJson(JsonReader reader, Type objectType, DbObjectId? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonToken.String)
        {
            var stringValue = reader.Value?.ToString();

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

        throw new JsonSerializationException($"Unexpected token type '{reader.TokenType}' when parsing nullable DbObjectId. Expected String or Null.");
    }

    public override void WriteJson(JsonWriter writer, DbObjectId? value, JsonSerializer serializer)
    {
        if (value == null || value.Value.IsEmpty)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value.Value.Value);
        }
    }
}
