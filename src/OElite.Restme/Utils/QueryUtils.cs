using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace OElite.Restme.Utils
{
    public static class QueryUtils
    {
        public static NameValueCollection IdentifyQueryParams(this string value)
        {
            NameValueCollection result = new NameValueCollection();
            var paramIndex = value?.IndexOf('?');
            if (paramIndex >= 0)
            {
                var paramPairs = value.Substring(paramIndex.GetValueOrDefault() + 1).Split('&');
                foreach (var pair in paramPairs)
                {
                    var pairArray = pair.Split('=');
                    if (pairArray?.Length == 2)
                    {
                        var kKey = pairArray[0].Trim();
                        var kValue = WebUtility.UrlDecode(pairArray[1].Trim());
                        result.Set(kKey, kValue);
                    }
                }
            }
            return result;
        }
        public static string ParseIntoQueryString(this NameValueCollection values, bool includeQuestionMark = true)
        {
            string result = null;
            if (values?.Count > 0)
            {
                int index = 0;
                foreach (var k in values.Keys)
                {
                    result = index == 0 ? $"{k}={WebUtility.UrlEncode(values[k.ToString()])}" : $"&{k}={WebUtility.UrlEncode(values[k.ToString()])}";
                }
            }
            if (includeQuestionMark)
                result = $"?{result}";

            return result;
        }
    }
}
