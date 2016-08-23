using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OElite.UnitTests
{
    public class RestmeAzureStorageTests
    {
        private string storageConnectionString = null;
        public RestmeAzureStorageTests()
        {

            var configBuilder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("testSettings.json", true);
            storageConnectionString = configBuilder.Build()["azureStorageConnectionString"];
        }

        [Fact]
        public void GetTests()
        {
            if (storageConnectionString.IsNotNullOrEmpty())
            {
                var filePath = "/private/storage/fileitems/0001d4ce-7670-425b-b609-46bfdf4afb07-data.json";
                var rest = new Rest(storageConnectionString);
                var stream = rest.Get<MemoryStream>(filePath);
                Assert.True(stream?.Length > 0);
                var jsonData = rest.Get<string>(filePath);
                Assert.True(jsonData?.ToLower()?.Contains("createdon"));
            }
        }
        [Fact]
        public void PostAndDeleteTests()
        {
            if (storageConnectionString.IsNotNullOrEmpty())
            {
				var rest = new Rest(storageConnectionString);
                var testPath1 = "/private/test/test.json";
                var testPath2 = "/private/test/testStream.stream";

                var testData = new GeoResult() { City = "London", Country_Code = "GB", Country_Name = "United Kingdom" };
                var testDataInStream = StringUtils.GenerateStreamFromString(testData.JsonSerialize());
                var result1 = rest.Post<GeoResult>(testPath1, testData);
                Assert.True(result1?.City == "London");
                Assert.True(rest.Delete<bool>(testPath1));

                var result2 = rest.Post<Stream>(testPath2, testDataInStream);
                Assert.True(result2?.Length > 0);
                Assert.True(rest.Delete<bool>(testPath2));

            }

        }

        private class GeoResult
        {
            public string IP { get; set; }
            public string Country_Code { get; set; }
            public string Country_Name { get; set; }
            public string Region_Code { get; set; }
            public string Region_Name { get; set; }
            public string City { get; set; }
            public string Zip_Code { get; set; }
            public string Time_Zone { get; set; }
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
            public string Metro_Code { get; set; }
        }

    }
}
