using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OElite.UnitTests
{
    public class RestmeRedisTests
    {
        private readonly string _redisConnectionString = null;
        public RestmeRedisTests()
        {

            var configBuilder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("testSettings.json", true);
            _redisConnectionString = configBuilder.Build()["redisConnectionString"];
        }

        [Fact]
        public void GetTests()
        {
            if (!_redisConnectionString.IsNotNullOrEmpty()) return;
            var rest = new Rest(_redisConnectionString);
            rest.Post<string>("home:test1", "city");
            var stringData = rest.Get<string>("home:test1");
            Assert.True(stringData?.ToLower().Contains("city"));

            rest.Post<GeoResult>("home:test2", new GeoResult() { City = "London", Country_Code = "GB", Country_Name = "United Kingdom" });
            var objData = rest.Get<GeoResult>("home:test2");
            Assert.True(objData?.City == "London");
        }
        [Fact]
        public void PostAndDeleteTests()
        {
            if (!_redisConnectionString.IsNotNullOrEmpty()) return;
            var rest = new Rest(_redisConnectionString);
            var testPath1 = "home:test1";
            var testPath2 = "home:test2";

            var testData = new GeoResult() { City = "London", Country_Code = "GB", Country_Name = "United Kingdom" };
            var testDataInStream = StringUtils.GenerateStreamFromString(testData.JsonSerialize());
            var result1 = rest.Post<GeoResult>(testPath1, testData);
            Assert.True(result1?.City == "London");
            Assert.True(rest.Delete<bool>(testPath1));

            var result2 = rest.Post<Stream>(testPath2, testDataInStream);
            Assert.True(result2?.Length > 0);
            Assert.True(rest.Delete<bool>(testPath2));
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
