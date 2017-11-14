using System;

namespace OElite
{
    public class BooleanUtils
    {
        public static bool GetBooleanValueFromObject(object objectValue, bool boolReturnedIfFailed = false)
        {
            if (objectValue != null)
            {
                try
                {
                    if (objectValue.GetType() == typeof(string) && StringUtils.GetStringValueOrEmpty(objectValue) == "1")
                        return true;
                    return Convert.ToBoolean(objectValue);
                }
                catch (Exception)
                {
                    return boolReturnedIfFailed;
                }
            }

            return boolReturnedIfFailed;
        }
    }
}
