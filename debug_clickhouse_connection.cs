using System;
using OElite.Restme.ClickHouse;

class Program
{
    static void Main()
    {
        var testConnectionString = "clickhouse://clickhouse:clickhouse@localhost:65151/default";
        Console.WriteLine($"Input: {testConnectionString}");

        // Test the connection string parsing
        var wrapper = new ClickHouseConnectionWrapper(testConnectionString);
        Console.WriteLine("Connection wrapper created successfully");
    }
}