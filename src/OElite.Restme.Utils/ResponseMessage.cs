using System;
using System.Collections;

namespace OElite
{
    public class ResponseMessage
    {
        public object Data { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }

        public string AssociatedTotalCountPropertyName { get; set; }
        public ResponseMessage()
        {
            AssociatedTotalCountPropertyName = "TotalRecordsCount";
        }
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
                Total = (((ICollection)data)?.Count).GetValueOrDefault();
            }
        }
    }

    public static class ResponseMessageExtensions
    {
        public static T GetOriginalData<T>(this ResponseMessage msg)
        {
            if (msg == null) return default(T);
            T result = default(T);
            if ((msg.Data is string && typeof(T) != typeof(string)) ||
                (msg.Data is Newtonsoft.Json.Linq.JArray && typeof(T) != typeof(Newtonsoft.Json.Linq.JArray)))
            {
                result = msg.Data.ToString().JsonDeserialize<T>(false);
            }
            else
                result = (T)Convert.ChangeType(msg.Data, typeof(T));

            if (result != null)
            {
                var propInfo = typeof(T).GetProperty(msg.AssociatedTotalCountPropertyName);
                if (propInfo != null)
                    result.SetPropertyValue(msg.AssociatedTotalCountPropertyName, msg.Total);
            }
            return result;
        }
    }
}