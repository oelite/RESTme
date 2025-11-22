using System;
using System.Linq;
using OElite;

class Program 
{
    static void Main()
    {
        // Create the same anonymous object as the test
        var largeObject = new
        {
            Id = Guid.NewGuid(),
            Name = "Large Test Object for S3",
            Data = new byte[1024 * 10], // 10KB of data
            NestedObjects = Enumerable.Range(0, 100).Select(i => new
            {
                Index = i,
                Value = $"Item {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            }).ToArray()
        };
        
        // Serialize it
        var jsonString = StringUtils.JsonSerialize(largeObject);
        Console.WriteLine($"Serialized JSON length: {jsonString.Length}");
        Console.WriteLine($"First 200 chars: {jsonString.Substring(0, Math.Min(200, jsonString.Length))}");
        
        // Try to deserialize as dynamic
        var deserialized = StringUtils.JsonDeserialize<dynamic>(jsonString);
        Console.WriteLine($"Deserialized result is null: {deserialized == null}");
        
        if (deserialized != null)
        {
            Console.WriteLine($"Has Id property: {deserialized.Id != null}");
        }
    }
}
