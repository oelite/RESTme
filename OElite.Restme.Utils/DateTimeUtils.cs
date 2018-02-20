using System;
using System.Collections.Generic;
using System.Linq;

namespace OElite
{
    public static class DateTimeUtils
    {
        public static DateTime CannotBeDefaultDateTime(object objectValue)
        {
            var result = GetDateTimeFromObjectValue(objectValue);
            if (result == null || result == DateTime.MinValue || result == DateTime.MaxValue)
                throw new OEliteException("Null, minimum datetime value or maximum datetime value are not allowed.");
            else
                return result;
        }

        public static DateTime GetDateTimeFromObjectValue(object objectValue)
        {
            if (objectValue == null) return DateTime.MinValue;
            try
            {
                var dateTime = Convert.ToDateTime(objectValue);
                return dateTime;
            }
            catch (Exception ex)
            {
                //OEliteHelper.Logger.Info("Failed to convert object with type of [ " + objectValue.GetType() + " ] into a DateTime value", ex);
            }
            return DateTime.MinValue;
        }
        public static DateTime GetUtcDateTimeFromObjectValue(object objectValue)
        {
            return GetDateTimeFromObjectValue(objectValue).ToUniversalTime();
        }
        public static DateTime GetLocalDateTimeFromObjectValue(object objectValue)
        {
            return GetDateTimeFromObjectValue(objectValue).ToLocalTime();
        }

        public static bool IsValidSqlDateTimeValue(object objectValue)
        {
            var result = GetDateTimeFromObjectValue(objectValue);
            return result != DateTime.MaxValue && result != DateTime.MinValue && result.Year >= 1753 && result.Year < 9999;
        }
        public static bool IsValidSqlDateTime(this DateTime dateTime)
        {
            return IsValidSqlDateTimeValue(dateTime);
        }
        public static bool IsValidSqlDateTime(this DateTime? dateTime)
        {
            return IsValidSqlDateTimeValue(dateTime);
        }

        /// <summary>
        /// Retrieves a System.TimeZoneInfo object from the registry based on its identifier.
        /// </summary>
        /// <param name="id">The time zone identifier, which corresponds to the System.TimeZoneInfo.Id property.</param>
        /// <returns>A System.TimeZoneInfo object whose identifier is the value of the id parameter.</returns>
        public static TimeZoneInfo FindTimeZoneById(string id)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        /// <summary>
        /// Returns a sorted collection of all the time zones
        /// </summary>
        /// <returns>A read-only collection of System.TimeZoneInfo objects.</returns>
        public static List<TimeZoneInfo> GetSystemTimeZones()
        {
            return TimeZoneInfo.GetSystemTimeZones().ToList();
        }

    }
}
