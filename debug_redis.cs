using System;
using System.Threading.Tasks;
using StackExchange.Redis;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Testing Redis empty string handling directly...");

        using var redis = ConnectionMultiplexer.Connect("localhost:6379");
        var db = redis.GetDatabase();

        // Test 1: Store and retrieve empty string
        await db.StringSetAsync("test:empty", "");
        var result = await db.StringGetAsync("test:empty");

        Console.WriteLine($"HasValue: {result.HasValue}");
        Console.WriteLine($"Value: '{result}'");
        Console.WriteLine($"Length: {result.ToString().Length}");
        Console.WriteLine($"IsNull: {result.IsNull}");
        Console.WriteLine($"IsNullOrEmpty: {result.IsNullOrEmpty}");

        // Test 2: Compare with regular string
        await db.StringSetAsync("test:regular", "hello");
        var result2 = await db.StringGetAsync("test:regular");

        Console.WriteLine($"\nRegular string:");
        Console.WriteLine($"HasValue: {result2.HasValue}");
        Console.WriteLine($"Value: '{result2}'");

        // Test 3: What if we don't store anything
        var result3 = await db.StringGetAsync("test:nonexistent");
        Console.WriteLine($"\nNon-existent key:");
        Console.WriteLine($"HasValue: {result3.HasValue}");
        Console.WriteLine($"Value: '{result3}'");
    }
}