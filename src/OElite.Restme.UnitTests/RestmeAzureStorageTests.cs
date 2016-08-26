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
                var filePath = "/test/test.json";
                var rest = new Rest(storageConnectionString);
                var stream = rest.Get<GeoResult>(filePath);
                if (stream == null)
                {
                    var testData = new GeoResult() { City = "London", Country_Code = "GB", Country_Name = "United Kingdom" };
                    rest.Post<GeoResult>(filePath, testData);
                    stream = rest.Get<GeoResult>(filePath);
                }
                Assert.True(stream != null);
                var retirmData = rest.Get<GeoResult>(filePath);
                Assert.True(retirmData?.City?.IsNotNullOrEmpty());
            }
        }

        [Fact]
        public void AddAndPost()
        {
            if (storageConnectionString.IsNotNullOrEmpty())
            {
                var rest = new Rest(storageConnectionString);
                var testPath1 = "/test/test_addAndPost.json";

                var testData = new GeoResult() { City = "London", Country_Code = "GB", Country_Name = "United Kingdom" };
                rest.Add(testData);
                var result1 = rest.Post<GeoResult>(testPath1);
                Assert.True(result1?.City == "London");
                Assert.True(rest.Delete<bool>(testPath1));
            }
        }
        [Fact]
        public void PostAndDeleteTests()
        {
            if (storageConnectionString.IsNotNullOrEmpty())
            {
                var rest = new Rest(storageConnectionString);
                var testPath1 = "/test/test.json";
                var testPath2 = "/test/test.stream";

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
