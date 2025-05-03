using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OElite.Restme.GoogleUtils.Models;

namespace OElite.Restme.GoogleUtils
{
    public class GeocodingUtils
    {
        public const string RequestUrl = "https://maps.googleapis.com/maps/api/geocode/";


        public static async Task<GeoAddress?> GetGeoAddressAsync(string apiKey, string originAddress,
            GeoUnit geoUnit = GeoUnit.Metric, RequestOutputFormat outputFormat = RequestOutputFormat.Json)
        {
            if (apiKey.IsNotNullOrEmpty() && originAddress.IsNotNullOrEmpty())
            {
                var path = $"{(outputFormat == RequestOutputFormat.Json ? "json" : "xml")}?key={apiKey}";
                if (geoUnit != GeoUnit.Metric)
                    path += "&units=imperial";

                path += $"&address={originAddress}";
                using var rest = new Rest(new Uri(RequestUrl));
                var result = await rest.GetAsync<string>(path);
                if (!result.IsNotNullOrEmpty()) return null;
                if (result == null) return null;
                var jObject = JObject.Parse(result);
                if (!jObject.ContainsKey("results")) return null;
                GeoAddress?[]? valueResult = jObject["results"]?.ToObject<GeoAddress[]>();
                if (valueResult?.Length > 0)
                {
                    return valueResult[0];
                }
            }

            return null;
        }
    }
}