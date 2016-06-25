using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OElite
{
    public static class GuidUtils
    {
        public static Guid GetGuidOrEmpty(object value)
        {
            if (value != null)
            {
                try
                {
                    if (value.GetType() == typeof(string))
                        return new Guid(StringUtils.GetStringValueOrEmpty(value));
                    else
                        return (Guid)value;
                }
                catch (Exception ex)
                {
                    return Guid.Empty;
                }
            }
            else
                return Guid.Empty;
        }
        public static bool IsNullOrEmpty(this Guid value)
        {
            if (value == Guid.Empty)
                return true;
            else
                return false;
        }
        public static bool IsNotNullOrEmpty(this Guid value)
        {
            return !IsNullOrEmpty(value);
        }
        public static bool IsNotNullOrEmpty(this Guid? value)
        {
            return !IsNullOrEmpty(value);
        }

        public static bool IsNullOrEmpty(this Guid? value)
        {
            if (value == null || value == Guid.Empty)
                return true;
            else
                return false;
        }
    }
}
