using System.Collections.Generic;
using System.Net;

namespace OElite
{
    public static class QueryUtils
    {
        public static Dictionary<string, string> IdentifyQueryParams(this string value)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            var paramIndex = value?.IndexOf('?');
            if (!(paramIndex >= 0)) return result;

            var paramPairs = value.Substring(paramIndex.GetValueOrDefault() + 1).Split('&');
            foreach (var pair in paramPairs)
            {
                var pairArray = pair.Split('=');
                if (pairArray?.Length != 2) continue;
                var kKey = pairArray[0].Trim();
                var kValue = WebUtility.UrlDecode(pairArray[1].Trim());
                result.Add(kKey, kValue);
            }
            return result;
        }

        public static string ParseIntoQueryString(this Dictionary<string, string> values,
            bool includeQuestionMark = true)
        {
            string result = null;
            if (values?.Count > 0)
            {
                var index = 0;
                foreach (var k in values.Keys)
                {
                    result = index == 0
                        ? $"{k}={WebUtility.UrlEncode(values[k])}"
                        : $"&{k}={WebUtility.UrlEncode(values[k])}";
                    index++;
                }
            }
            if (includeQuestionMark)
                result = $"?{result}";

            return result;
        }
    }
}