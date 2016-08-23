using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                    string valueString = StringUtils.GetStringValueOrEmpty(objectValue);
                    if (valueString.IsNullOrEmpty())
                        return 0;
                    int dotIndex = valueString.IndexOf('.');
                    if (dotIndex > 0)
                        valueString = valueString.Substring(0, dotIndex);
                    int value = Int32.Parse(valueString);
                    return value;
                }
                catch (Exception ex)
                {
                    //OEliteHelper.Logger.Info(String.Format("Failed converting object [{0}] into integer value, 0 will be returned. ", objectValue.GetType()), ex);
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
                    //OEliteHelper.Logger.Info(String.Format("Failed converting object [{0}] into integer value, 0 will be returned. ", objectValue.GetType()), ex);
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
                    //OEliteHelper.Logger.Info(String.Format("Failed converting object [{0}] into decimal value, 0 will be returned. ", objectValue.GetType()), ex);
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
                else if (result is long)
                    return GetLongIntegerValueFromObject(val);
                else if (result is decimal)
                    return GetDecimalValueFromObject(val);
                else
                    return val;
            }
            catch (Exception ex)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    //OEliteHelper.Logger.Error("value cannot be converted into numeric values.", ex);
                });
                throw ex;
            }
        }

        public static int[] GetIntegerArrayFromString(string stringToConvert, string[] splitters)
        {
            List<int> result = new List<int>();
            if (!string.IsNullOrEmpty(stringToConvert) && splitters != null)
            {
                string[] values = stringToConvert.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                if (values != null && values.Length > 0)
                {
                    foreach (string value in values)
                    {
                        result.Add(NumericUtils.GetIntegerValueFromObject(value));
                    }
                }
            }
            return result.ToArray();
        }
        public static long[] GetLongIntArrayFromString(string stringToConvert, string[] splitters)
        {
            List<long> result = new List<long>();
            if (!string.IsNullOrEmpty(stringToConvert) && splitters != null)
            {
                string[] values = stringToConvert.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
                if (values != null && values.Length > 0)
                {
                    foreach (string value in values)
                    {
                        result.Add(NumericUtils.GetLongIntegerValueFromObject(value));
                    }
                }
            }
            return result.ToArray();
        }

        public static bool ArrayContainsAny(int[] valuesToValidate, int[] valuesToContain, int minimumCounts)
        {
            int counter = 0;
            if (valuesToValidate == null || valuesToValidate.Length < minimumCounts) return false;

            foreach (int value in valuesToContain)
            {
                if (valuesToValidate.Contains(value))
                    counter++;
                if (counter >= minimumCounts) return true;
            }
            return false;
        }

    }
}
