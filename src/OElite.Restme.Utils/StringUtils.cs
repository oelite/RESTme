using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
//using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace OElite
{
    public class StringUtils
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Validate an input whether it is null or empty string
        /// </summary>
        /// <param name="stringValue">string value to validate</param>
        /// <returns>return the original string if it passes</returns>
        /// <exception cref="WebCider.OEliteException" > throws when the string is invalid </exception>
        public static string CanNotBeNullOrEmpty(string stringValue, bool trim = true)
        {
            if (string.IsNullOrEmpty(stringValue) || (trim && string.IsNullOrEmpty(stringValue.Trim())))
            {
                logger.Error("The string value requested is null or empty which is not allowed for the current process");
                throw new OEliteException(
                    "The string value requested is null or empty which is not allowed for the current process");
            }
            else return stringValue.Trim();
        }

        public static string CanNotBeNullOrEmpty(object stringObj, bool trim = true)
        {
            return CanNotBeNullOrEmpty(GetStringValueOrEmpty(stringObj, trim));
        }

        /// <summary>
        /// A simple subString method to format a string cut
        /// </summary>
        /// <param name="original"></param>
        /// <param name="length"></param>
        /// <param name="appendix"></param>
        /// <returns></returns>
        public static string SubString(string original, int length, string appendix)
        {
            string newString = "";
            if (original.Length <= length)
            {
                newString = original;
            }
            else
            {
                newString = original.Substring(0, length) + appendix;
            }
            return newString;
        }

        public static string GetFullClassNameFromObject(object objectValue)
        {
            return GetFullClassNameFromType(objectValue.GetType());
        }

        public static string GetFullClassNameFromType(Type type)
        {
            return type.Name;
        }

        public static string GetStringFromStream(System.IO.Stream stream)
        {
            return GetStringFromStream(stream, Encoding.UTF8);
        }

        public static string GetStringFromStream(System.IO.Stream stream, Encoding encoding)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            try
            {
                stream.Position = 0;
                using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                //OEliteHelper.Logger.Info("Failed Converting Stream value into String value: " + ex.Message, ex);
                return string.Empty;
            }
        }

        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static string GetStringValueOrEmpty(object objectValue, bool trim = true)
        {
            if (objectValue != null)
            {
                try
                {
                    string result = Convert.ToString(objectValue);
                    if (result == objectValue.GetType().ToString()) return string.Empty;
                    else return trim ? result.Trim() : result;
                }
                catch (Exception ex)
                {
                    //OEliteHelper.Logger.Info(String.Format("Failed Converting object value [typeof: {0} ] into String value: {1}", objectValue.GetType(), ex.Message), ex);
                    return string.Empty;
                }
            }
            else return string.Empty;
        }

        public static bool StringContains(string stringToValidate, string[] stringsToAttempt, string[] splitters,
            int minimumIncludes)
        {
            if (!string.IsNullOrEmpty(stringToValidate))
            {
                string[] values = stringToValidate.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                return StringContains(values, stringsToAttempt, minimumIncludes);
            }
            else return true;
        }

        public static bool StringContains(string[] strings, string[] stringsToAttempt, int minimumIncludes)
        {
            int counter = 0;
            if (strings != null && strings.Length > 0)
            {
                foreach (string role in stringsToAttempt)
                {
                    if (strings.Contains(role)) counter++;
                    if (counter >= minimumIncludes) return true;
                }
            }
            return false;
        }

        public static bool StringContains(string stringToValidate, int[] idsToAttempt, string[] splitters,
            int minimumIncludes)
        {
            if (!string.IsNullOrEmpty(stringToValidate))
            {
                int[] values = NumericUtils.GetIntegerArrayFromString(stringToValidate, splitters);
                return NumericUtils.ArrayContainsAny(values, idsToAttempt, minimumIncludes);
            }
            return false;
        }

        /// <summary>
        /// The Id is very unique however may still be possible to duplication
        /// </summary>
        /// <returns></returns>
        public static string GenerateRandomId()
        {
            long i = 1;
            foreach (byte b in Guid.NewGuid().ToByteArray())
            {
                i *= ((int) b + 1);
            }
            return string.Format("{0:x}", i - DateTime.Now.Ticks);
        }

        /// <summary>
        /// Random declaration must be done outside the method to actually generate random numbers
        /// </summary>
        private static readonly Random Random = new Random();

        /// <summary>
        /// Generate passwords
        /// </summary>
        /// <param name="passwordLength"></param>
        /// <param name="strongPassword"> </param>
        /// <returns></returns>
        public static string PasswordGenerator(int passwordLength, bool strongPassword)
        {
            int seed = Random.Next(1, int.MaxValue);
            //const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZ0123456789";
            const string allowedChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNOPQRSTUVWXYZ0123456789";
            const string specialCharacters = @"!#$%&'()*+,-./:;<=>?@[\]_";

            var chars = new char[passwordLength];
            var rd = new Random(seed);

            for (var i = 0; i < passwordLength; i++)
            {
                // If we are to use special characters
                if (strongPassword && i % Random.Next(3, passwordLength) == 0)
                {
                    chars[i] = specialCharacters[rd.Next(0, specialCharacters.Length)];
                }
                else
                {
                    chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
                }
            }

            return new string(chars);
        }

        #region Web Related

        public static string ToSeoFriendly(string title, int maxLength = -1)
        {
            var match = Regex.Match(title.ToLower(), "[\\w]+");
            StringBuilder result = new StringBuilder("");
            bool maxLengthHit = false;
            while (match.Success && !maxLengthHit)
            {
                if (result.Length + match.Value.Length <= maxLength || maxLength <= 0)
                {
                    result.Append(match.Value + "-");
                }
                else
                {
                    maxLengthHit = true;
                    // Handle a situation where there is only one word and it is greater than the max length.
                    if (result.Length == 0) result.Append(match.Value.Substring(0, maxLength));
                }
                match = match.NextMatch();
            }
            // Remove trailing '-'
            if (result[result.Length - 1] == '-') result.Remove(result.Length - 1, 1);
            return result.ToString();
        }

        #endregion

        public static string JsonSerialize(object value,
            Newtonsoft.Json.JsonSerializerSettings serializerSettings = null)
        {
            if (value == null)
                return string.Empty;
            try
            {
                //return ServiceStack.Text.JsonSerializer.SerializeToString(value, value.GetType());
                return Newtonsoft.Json.JsonConvert.SerializeObject(value, serializerSettings
                                                                          ??
                                                                          new Newtonsoft.Json.JsonSerializerSettings()
                                                                          {
                                                                              ContractResolver =
                                                                                  new OEliteJsonResolver()
                                                                          });
            }
            catch (Exception ex)
            {
                //OEliteHelper.Logger.Error("JSON serializing an object type of " + (value != null ? value.GetType().Name : "null :" + ex.Message), ex);
                return string.Empty;
            }
        }

        public static T JsonDeserialize<T>(string value,
            Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings = null)
        {
            try
            {
                if (value.IsNotNullOrEmpty())
                {
                    if (typeof(T).IsPrimitiveType())
                        return (T) Convert.ChangeType(value, typeof(T));
                    else
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value,
                            jsonSerializerSettings ??
                            new Newtonsoft.Json.JsonSerializerSettings() {ContractResolver = new OEliteJsonResolver()});
                }
                else
                    return default(T);
            }
            catch (Exception)
            {
                //OEliteHelper.Logger.Warn($"Deserializing an object to type {typeof(T).FullName} has failed. The original value was: \n {value}", ex);
            }
            return default(T);
        }


        public static class ByXPath
        {
            public const string AnchorTag = "a";
            public const string AnyNode = "*";

            public enum Comparison
            {
                Presence,
                Equality,
                Contains,
                StartsWith,
                EndsWith,
            }

            public static string XPath(string xpath)
            {
                return xpath;
            }

            public static string Tag(string tagName)
            {
                return string.Format(@"//{0}", tagName);
            }

            public static string Id(string id)
            {
                return XPath(Comparison.Equality, "id", id);
            }

            public static string ClassName(string name, Comparison type = Comparison.Equality)
            {
                return XPath(type, "class", name);
            }

            public static string Link(string linkText, Comparison comparisonType = Comparison.Equality)
            {
                return XPath(comparisonType, "href", linkText, AnchorTag);
            }

            public static string PartialLink(string linkText)
            {
                return Link(linkText, Comparison.Contains);
            }

            public static string Attribute(string attribute, string name,
                Comparison comparisonType = Comparison.Equality, string node = null)
            {
                return XPath(comparisonType, attribute, name, node);
            }

            public static string XPath(Comparison compareBy, string attribute, string attributeValue = "",
                string node = null)
            {
                switch (compareBy)
                {
                    case Comparison.Equality:
                        return string.Format(@"//{0}[@{1}]", node ?? AnyNode, attribute);

                    case Comparison.Presence:
                        return string.Format(@"//{0}[@{1}=""{2}""]", node ?? AnyNode, attribute, attributeValue);

                    case Comparison.Contains:
                        return XpathFunction(node, "contains", attribute, attributeValue);

                    case Comparison.StartsWith:
                        return XpathFunction(node, "starts-with", attribute, attributeValue);

                    case Comparison.EndsWith:
                        return XpathFunction(node, "ends-with", attribute, attributeValue);

                    default:
                        return string.Empty;
                }
            }

            private static string XpathFunction(string node, string func, string attr, string name)
            {
                return string.Format(@"//{0}[{1}(@{2}=""{3}"")]", node ?? AnyNode, func, attr, name);
            }
        }


        public static string ByteSizeInString(long byteSize)
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            const string format = "#,0.0";

            if (byteSize < 1024)
                return byteSize.ToString("#,0", culture);
            byteSize /= 1024;
            if (byteSize < 1024)
                return byteSize.ToString(format, culture) + " KB";
            byteSize /= 1024;
            if (byteSize < 1024)
                return byteSize.ToString(format, culture) + " MB";
            byteSize /= 1024;
            if (byteSize < 1024)
                return byteSize.ToString(format, culture) + " GB";
            byteSize /= 1024;
            return byteSize.ToString(format, culture) + " TB";
        }
    }
}