using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OElite;

namespace ConsoleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("============== Http Client Test ==========");
            var rest = new Rest(new Uri("http://freegeoip.net"));
            var result1 = rest.Get<string>("/json/github.com");
            Console.WriteLine($"return results:\n {result1}");

            Console.WriteLine("============== Azure Storage Client Test ==========");
            var storageConnectionString = "";
            if (storageConnectionString.IsNotNullOrEmpty())
            {
                var filePath = "/test/0001d4ce-7670-425b-b609-46bfdf4afb07-data.json";
                var rest2 = new Rest(storageConnectionString);
                var returnRest2 = rest2.Post<string>(filePath, "some data");
                Console.WriteLine(returnRest2 == "some data"
                    ? $"return value matches with original and is now stored on azure storage."
                    : "returned expected values");
            }
            else
                Console.Write("azure connection string not configured.\n");

            Console.WriteLine("============== Redis Client Test ==========");
            var redisConnectionString = "";
            if (redisConnectionString.IsNotNullOrEmpty())
            {
                var rest3 = new Rest(redisConnectionString);
                rest3.Post<string>("home:test1", "city");
                Console.WriteLine("saved data to Redis server.\n");
                var stringData = rest.Get<string>("home:test1");
                Console.WriteLine($"attempted to requested the value just saved, and returned: {stringData}");

            }
            else
                Console.Write("redis connection string not configured.\n");

        }
    }
}
