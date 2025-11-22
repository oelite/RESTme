using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OElite.Restme.Redis.IntegrationTests.Models;

/// <summary>
/// Simple test model for basic cache operations
/// </summary>
public class SimpleUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public override bool Equals(object? obj)
    {
        if (obj is not SimpleUser other) return false;
        return Id == other.Id && Name == other.Name && Email == other.Email && Age == other.Age;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Email, Age);
    }
}

/// <summary>
/// Complex test model with nested objects and collections
/// </summary>
public class ComplexOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";

    public Address ShippingAddress { get; set; } = new();
    public Address BillingAddress { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();

    public PaymentInfo? Payment { get; set; }
    public List<string> Tags { get; set; } = new();

    public override bool Equals(object? obj)
    {
        if (obj is not ComplexOrder other) return false;
        return OrderId == other.OrderId &&
               CustomerId == other.CustomerId &&
               Status == other.Status &&
               TotalAmount == other.TotalAmount &&
               Currency == other.Currency &&
               Items.Count == other.Items.Count;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(OrderId, CustomerId, Status, TotalAmount);
    }
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
}

public class PaymentInfo
{
    public string PaymentId { get; set; } = string.Empty;
    public PaymentMethod Method { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6
}

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    PayPal = 2,
    BankTransfer = 3,
    Cryptocurrency = 4,
    Cash = 5
}

/// <summary>
/// Large test model for performance and memory testing
/// </summary>
public class LargeDataModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Large string data to test serialization performance
    public string LargeDescription { get; set; } = string.Empty;

    // Large collection to test collection serialization
    public List<DataPoint> DataPoints { get; set; } = new();

    // Binary data simulation (base64 encoded)
    public string BinaryData { get; set; } = string.Empty;

    // Nested complex objects
    public Dictionary<string, NestedData> NestedObjects { get; set; } = new();

    /// <summary>
    /// Creates a large data model with specified size for performance testing
    /// </summary>
    public static LargeDataModel CreateWithSize(int dataPointCount = 1000, int descriptionLength = 10000)
    {
        var model = new LargeDataModel
        {
            LargeDescription = new string('A', descriptionLength),
            BinaryData = Convert.ToBase64String(new byte[1024]) // 1KB of binary data
        };

        // Add data points
        for (int i = 0; i < dataPointCount; i++)
        {
            model.DataPoints.Add(new DataPoint
            {
                Index = i,
                Value = Random.Shared.NextDouble() * 1000,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                Category = $"Category_{i % 10}",
                Properties = new Dictionary<string, string>
                {
                    ["prop1"] = $"value_{i}_1",
                    ["prop2"] = $"value_{i}_2",
                    ["prop3"] = $"value_{i}_3"
                }
            });
        }

        // Add nested objects
        for (int i = 0; i < 50; i++)
        {
            model.NestedObjects[$"nested_{i}"] = new NestedData
            {
                Name = $"Nested Object {i}",
                Data = Enumerable.Range(0, 100).Select(x => $"data_{i}_{x}").ToList()
            };
        }

        return model;
    }

    /// <summary>
    /// Estimates the approximate size of this model when serialized
    /// </summary>
    public long EstimateSize()
    {
        var baseSize = LargeDescription.Length * 2; // Unicode characters
        var dataPointsSize = DataPoints.Count * 200; // Approximate size per data point
        var binaryDataSize = BinaryData.Length;
        var nestedSize = NestedObjects.Values.Sum(x => x.EstimateSize());

        return baseSize + dataPointsSize + binaryDataSize + nestedSize;
    }
}

public class DataPoint
{
    public int Index { get; set; }
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class NestedData
{
    public string Name { get; set; } = string.Empty;
    public List<string> Data { get; set; } = new();

    public long EstimateSize()
    {
        return Name.Length * 2 + Data.Sum(x => x.Length * 2);
    }
}

/// <summary>
/// Test model with special characters and edge cases for serialization testing
/// </summary>
public class SpecialCharacterModel
{
    public string Id { get; set; } = string.Empty;

    // Unicode and special characters
    public string UnicodeText { get; set; } = "Hello 世界 🌍 Здравствуй мир";
    public string JsonSpecialChars { get; set; } = "Quote: \" Backslash: \\ Newline: \n Tab: \t";
    public string UrlEncodedData { get; set; } = "param1=value%20with%20spaces&param2=special%21chars";

    // Edge case values
    public string EmptyString { get; set; } = string.Empty;
    public string SingleChar { get; set; } = "A";
    public string MaxLengthString { get; set; } = new string('X', 1000);

    // Nullable properties
    public string? NullableString { get; set; }
    public int? NullableInt { get; set; }
    public DateTime? NullableDateTime { get; set; }

    // Special numeric values
    public double InfiniteValue { get; set; } = double.PositiveInfinity;
    public double NanValue { get; set; } = double.NaN;
    public decimal MaxDecimal { get; set; } = decimal.MaxValue;
    public decimal MinDecimal { get; set; } = decimal.MinValue;
}

/// <summary>
/// Test model for TTL and expiration testing
/// </summary>
public class ExpiringData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ExpectedTtl { get; set; }
    public string TestCategory { get; set; } = string.Empty;

    public static ExpiringData CreateWithTtl(string content, TimeSpan ttl, string category = "default")
    {
        return new ExpiringData
        {
            Content = content,
            ExpectedTtl = ttl,
            TestCategory = category
        };
    }
}

/// <summary>
/// Test model for concurrent access testing
/// </summary>
public class ConcurrencyTestModel
{
    public string Id { get; set; } = string.Empty;
    public int Counter { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> OperationHistory { get; set; } = new();

    public void AddOperation(string operation)
    {
        OperationHistory.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {operation}");
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Performance metrics for cache operations
/// </summary>
public class CacheOperationMetrics
{
    public string OperationType { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long DataSize { get; set; }
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }

    public double ThroughputMBps => Success && Duration.TotalSeconds > 0
        ? (DataSize / (1024.0 * 1024.0)) / Duration.TotalSeconds
        : 0;
}