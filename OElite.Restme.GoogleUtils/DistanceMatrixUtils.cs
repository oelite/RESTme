using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OElite.Restme.GoogleUtils.Models;

namespace OElite.Restme.GoogleUtils
{
    public class DistanceMatrixUtils
    {
        public const string RequestUrl = "https://maps.googleapis.com/maps/api/distancematrix/";

        public static async Task<DistanceMatrixResponse?> GetDistanceAsync(string apiKey, List<string>? originAddresses,
            List<string>? destinationAddresses,
            GeoUnit geoUnit = GeoUnit.Metric, RequestOutputFormat outputFormat = RequestOutputFormat.Json)
        {
            if (!apiKey.IsNotNullOrEmpty() || !(originAddresses?.Count > 0) ||
                !(destinationAddresses?.Count > 0)) return null;
            var path = $"{(outputFormat == RequestOutputFormat.Json ? "json" : "xml")}?key={apiKey}";
            if (geoUnit != GeoUnit.Metric)
                path += "&units=imperial";
            var origins = string.Join("|", originAddresses.Select(item => item.Trim()));
            var destinations = string.Join("|", destinationAddresses.Select(item => item.Trim()));

            path += $"&origins={origins}&destinations={destinations}";
            using var rest = new Rest(baseUri: new Uri(RequestUrl));
            return await rest.GetAsync<DistanceMatrixResponse>(path);
        }

        public static async Task<DistanceMatrixDistance?> GetDistanceAsync(string apiKey, string originAddress,
            string destinationAddress, GeoUnit geoUnit = GeoUnit.Metric,
            RequestOutputFormat outputFormat = RequestOutputFormat.Json)
        {
            var result = await GetDistanceAsync(apiKey, [originAddress],
                [destinationAddress],
                geoUnit, outputFormat);
            if (result is { Status: "OK", Rows.Length: > 0 } && result.Rows[0].Elements is { Length: > 0 })
            {
                return result.Rows[0].Elements?[0];
            }

            return null;
        }

        public static async Task<List<DistanceMatrixDistance?>?> GetDistancesAsync(string apiKey, string originAddress,
            List<string> destinationAddresses, GeoUnit geoUnit = GeoUnit.Metric,
            RequestOutputFormat outputFormat = RequestOutputFormat.Json)
        {
            var result = await GetDistanceAsync(apiKey, [originAddress],
                destinationAddresses,
                geoUnit, outputFormat);
            if (result is not { Status: "OK", Rows.Length: > 0 } ||
                result.Rows[0].Elements is not { Length: > 0 }) return null;
            return result.Rows[0].Elements?.ToList();

            return null;
        }
    }
}