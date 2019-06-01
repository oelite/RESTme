using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OElite.Restme.GoogleUtils.Models;

namespace OElite.Restme.GoogleUtils
{
    public class GeocodingUtils
    {
        public const string RequestUrl = "https://maps.googleapis.com/maps/api/geocode/";


        public static Task<GeoAddress> GetGeoAddressAsync(string apiKey, string originAddress,
            GeoUnit geoUnit = GeoUnit.Metric, RequestOutputFormat outputFormat = RequestOutputFormat.Json)
        {
            if (apiKey.IsNotNullOrEmpty() && originAddress.IsNotNullOrEmpty())
            {
                var path = $"{(outputFormat == RequestOutputFormat.Json ? "json" : "xml")}?key={apiKey}";
                if (geoUnit != GeoUnit.Metric)
                    path += "&units=imperial";

                path += $"&address={originAddress}";
                using (var rest = new Rest(new Uri(RequestUrl)))
                {
                    return rest.GetAsync<GeoAddress>(path);
                }
            }

            return Task.FromResult(default(GeoAddress));
        }
    }
}