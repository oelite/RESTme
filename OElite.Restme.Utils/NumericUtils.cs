using System;
using System.Collections.Generic;
using System.Linq;
using OElite.Restme.Utils;

namespace OElite
{
    public class NumericUtils
    {
        public static int GetIntegerValueFromObject(object objectValue)
        {
            if (objectValue != null)
            {
                try
                {
                    var valueString = StringUtils.GetStringValueOrEmpty(objectValue);
                    if (valueString.IsNullOrEmpty())
                        return 0;
                    var dotIndex = valueString.IndexOf('.');
                    if (dotIndex > 0)
                        valueString = valueString.Substring(0, dotIndex);
                    var value = int.Parse(valueString);
                    return value;
                }
                catch (Exception ex)
                {
                    RestmeLogger.LogDebug(
                        String.Format("Failed converting object [{0}] into integer value, 0 will be returned. ",
                            objectValue.GetType()), ex);
                    return 0;
                }
            }
            else return 0;
        }

        public static long GetLongIntegerValueFromObject(object objectValue)
        {
            if (objectValue != null)
            {
                try
                {
                    return long.Parse(objectValue.ToString());
                }
                catch (Exception ex)
                {
                    RestmeLogger.LogDebug(
                        string.Format("Failed converting object [{0}] into integer value, 0 will be returned. ",
                            objectValue.GetType()), ex);
                    return 0;
                }
            }
            else return 0;
        }

        public static decimal GetDecimalValueFromObject(object objectValue)
        {
            if (objectValue != null)
            {
                try
                {
                    return decimal.Parse(objectValue.ToString());
                }
                catch (Exception ex)
                {
                    RestmeLogger.LogDebug(
                        string.Format("Failed converting object [{0}] into decimal value, 0 will be returned. ",
                            objectValue.GetType()), ex);
                    return 0;
                }
            }
            else return 0;
        }

        public static T MustBeGreaterThanZero<T>(dynamic val)
        {
            try
            {
                T result = default(T);
                if (val <= 0) throw new OEliteException("value cannot be less than or equal to 0.");
                if (result is int)
                {
                    return GetIntegerValueFromObject(val);
                }

                if (result is long)
                    return GetLongIntegerValueFromObject(val);
                return result is decimal ? (T) GetDecimalValueFromObject(val) : (T) val;
            }
            catch (Exception ex)
            {
                RestmeLogger.LogDebug(ex.Message, ex);
                throw ex;
            }
        }

        public static int[] GetIntegerArrayFromString(string stringToConvert, string[] splitters)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(stringToConvert) || splitters == null) return result.ToArray();
            var values = stringToConvert.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            if (values == null || values.Length <= 0) return result.ToArray();
            result.AddRange(values.Select(GetIntegerValueFromObject));

            return result.ToArray();
        }

        public static long[] GetLongIntArrayFromString(string stringToConvert, string[] splitters)
        {
            var result = new List<long>();
            if (string.IsNullOrEmpty(stringToConvert) || splitters == null) return result.ToArray();
            var values = stringToConvert.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            if (values == null || values.Length <= 0) return result.ToArray();
            result.AddRange(values.Select(GetLongIntegerValueFromObject));

            return result.ToArray();
        }

        public static bool ArrayContainsAny(int[] valuesToValidate, IEnumerable<int> valuesToContain, int minimumCounts)
        {
            var counter = 0;
            if (valuesToValidate == null || valuesToValidate.Length < minimumCounts) return false;

            foreach (var value in valuesToContain)
            {
                if (valuesToValidate.Contains(value))
                    counter++;
                if (counter >= minimumCounts) return true;
            }

            return false;
        }
    }
}