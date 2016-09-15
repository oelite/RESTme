using System;
using System.Collections;
using System.Reflection;

namespace OElite
{
    public class ResponseMessage
    {
        public object Data { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }

        public string AssociatedTotalCountPropertyName { get; set; }

        public ResponseMessage(string associatedTotalCountPropertyName = "TotalRecordsCount")
        {
            AssociatedTotalCountPropertyName = associatedTotalCountPropertyName.IsNullOrEmpty()
                ? "TotalRecordsCount"
                : associatedTotalCountPropertyName;
        }

        public ResponseMessage(object data, string message = "", bool success = true,
            string associatedTotalCountPropertyName = "TotalRecordsCount")
        {
            if (data == null)
                return;
            AssociatedTotalCountPropertyName = associatedTotalCountPropertyName.IsNullOrEmpty()
                ? "TotalRecordsCount"
                : associatedTotalCountPropertyName;

            var propInfo = data.GetType().GetProperty(AssociatedTotalCountPropertyName);
            Success = success;
            Data = data;
            Message = message;
            Total = propInfo != null ? NumericUtils.GetIntegerValueFromObject(propInfo.GetValue(data)) : 1;
            if (Total != 0) return;

            var list = data as IList;
            if (list != null)
            {
                Total = (list?.Count).GetValueOrDefault();
            }
            else if (data is ICollection)
            {
                Total = (((ICollection) data)?.Count).GetValueOrDefault();
            }
        }
    }

    public static class ResponseMessageExtensions
    {
        public static T GetOriginalData<T>(this ResponseMessage msg)
        {
            if (msg == null) return default(T);
            var result = (T) Convert.ChangeType(msg.Data, typeof(T));

            if (msg.Total <= 0) return default(T);
            var propInfo = typeof(T).GetProperty(msg.AssociatedTotalCountPropertyName);
            if (propInfo != null)
                msg.SetPropertyValue(msg.AssociatedTotalCountPropertyName, msg.Total);
            return default(T);
        }
    }
}