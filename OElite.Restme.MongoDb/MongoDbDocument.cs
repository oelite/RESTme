using System.Collections;
using System.Text.Json;

namespace OElite.Restme.MongoDb;

/// <summary>
/// MongoDB-free document representation that replaces BsonDocument
/// Provides a clean abstraction over database documents without MongoDB dependencies
/// </summary>
public class MongoDbDocument : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _data;

    public MongoDbDocument()
    {
        _data = new Dictionary<string, object?>();
    }

    public MongoDbDocument(Dictionary<string, object?> data)
    {
        _data = new Dictionary<string, object?>(data);
    }

    public MongoDbDocument(IDictionary<string, object?> data)
    {
        _data = new Dictionary<string, object?>(data);
    }

    /// <summary>
    /// Creates a MongoDbDocument from JSON string
    /// </summary>
    public static MongoDbDocument FromJson(string json)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
        return new MongoDbDocument(data);
    }

    /// <summary>
    /// Converts the document to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_data);
    }

    /// <summary>
    /// Converts to Dictionary<string, object> for compatibility
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in _data)
        {
            if (kvp.Value != null)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a MongoDbDocument from Dictionary<string, object>
    /// </summary>
    public static MongoDbDocument FromDictionary(Dictionary<string, object> dictionary)
    {
        var data = new Dictionary<string, object?>();
        foreach (var kvp in dictionary)
        {
            data[kvp.Key] = kvp.Value;
        }
        return new MongoDbDocument(data);
    }

    /// <summary>
    /// Gets or sets a value by key
    /// </summary>
    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set => _data[key] = value;
    }

    /// <summary>
    /// Gets a value as a specific type
    /// </summary>
    public T? GetValue<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Sets a value for a key
    /// </summary>
    public void SetValue(string key, object? value)
    {
        _data[key] = value;
    }

    /// <summary>
    /// Checks if the document contains a key
    /// </summary>
    public bool ContainsKey(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Tries to get a value by key
    /// </summary>
    public bool TryGetValue(string key, out object? value) => _data.TryGetValue(key, out value);

    /// <summary>
    /// Removes a key from the document
    /// </summary>
    public bool Remove(string key) => _data.Remove(key);

    /// <summary>
    /// Adds a key-value pair to the document
    /// </summary>
    public void Add(string key, object? value) => _data.Add(key, value);

    /// <summary>
    /// Clears all data from the document
    /// </summary>
    public void Clear() => _data.Clear();

    /// <summary>
    /// Gets all keys in the document
    /// </summary>
    public ICollection<string> Keys => _data.Keys;

    /// <summary>
    /// Gets all values in the document
    /// </summary>
    public ICollection<object?> Values => _data.Values;

    /// <summary>
    /// Gets the number of key-value pairs in the document
    /// </summary>
    public int Count => _data.Count;

    /// <summary>
    /// Gets whether the document is read-only (always false)
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds a KeyValuePair to the document
    /// </summary>
    public void Add(KeyValuePair<string, object?> item) => _data.Add(item.Key, item.Value);

    /// <summary>
    /// Checks if the document contains a specific KeyValuePair
    /// </summary>
    public bool Contains(KeyValuePair<string, object?> item) => _data.Contains(item);

    /// <summary>
    /// Copies the document to an array
    /// </summary>
    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, object?>>)_data).CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Removes a specific KeyValuePair from the document
    /// </summary>
    public bool Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_data).Remove(item);

    /// <summary>
    /// Gets an enumerator for the document
    /// </summary>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _data.GetEnumerator();

    /// <summary>
    /// Gets a non-generic enumerator for the document
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();

    /// <summary>
    /// Implicit conversion from Dictionary<string, object>
    /// </summary>
    public static implicit operator MongoDbDocument(Dictionary<string, object> dictionary)
    {
        return FromDictionary(dictionary);
    }

    /// <summary>
    /// Implicit conversion to Dictionary<string, object>
    /// </summary>
    public static implicit operator Dictionary<string, object>(MongoDbDocument document)
    {
        return document.ToDictionary();
    }

    /// <summary>
    /// String representation of the document (JSON format)
    /// </summary>
    public override string ToString() => ToJson();

    /// <summary>
    /// Equality comparison
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is MongoDbDocument other)
        {
            return _data.Count == other._data.Count && _data.All(kvp => other._data.TryGetValue(kvp.Key, out var value) && Equals(kvp.Value, value));
        }
        return false;
    }

    /// <summary>
    /// Gets hash code for the document
    /// </summary>
    public override int GetHashCode() => _data.GetHashCode();
}