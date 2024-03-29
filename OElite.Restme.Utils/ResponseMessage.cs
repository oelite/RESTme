﻿using System;
using System.Collections;

namespace OElite
{
    public class ResponseMessage
    {
        public object Data { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }

        public DateTime CreatedOnUtc { get; set; }
        public DateTime ExpiryOnUtc { get; set; }
        public DateTime GraceTillUtc { get; set; }

        public string AssociatedTotalCountPropertyName { get; set; }

        public object MetaData { get; set; }

        public ResponseMessage()
        {
            AssociatedTotalCountPropertyName = "TotalRecordsCount";
            CreatedOnUtc = DateTime.UtcNow;
        }

        public ResponseMessage(string associatedTotalCountPropertyName = "TotalRecordsCount")
        {
            CreatedOnUtc = DateTime.UtcNow;
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
            MetaData = data.GetPropertyValue("MetaData");
            CreatedOnUtc = DateTime.UtcNow;
            Total = propInfo != null ? NumericUtils.GetIntegerValueFromObject(propInfo.GetValue(data)) : 1;
            if (Total != 0) return;

            switch (data)
            {
                case IList list:
                    Total = (list?.Count).GetValueOrDefault();
                    break;
                case ICollection _:
                    Total = (((ICollection)data)?.Count).GetValueOrDefault();
                    break;
            }
        }

        public static ResponseMessage SimpleFail => new ResponseMessage() { Success = false };
        public static ResponseMessage SimpleSuccess => new ResponseMessage(true);
    }

    public static class ResponseMessageExtensions
    {
        public static T GetOriginalData<T>(this ResponseMessage msg)
        {
            if (msg == null) return default(T);
            T result = default(T);
            if ((msg.Data is string && typeof(T) != typeof(string)) ||
                (msg.Data is Newtonsoft.Json.Linq.JObject) ||
                (msg.Data is Newtonsoft.Json.Linq.JArray && typeof(T) != typeof(Newtonsoft.Json.Linq.JArray)))
            {
                result = msg.Data.ToString().JsonDeserialize<T>(false);
            }
            else
                result = (T)Convert.ChangeType(msg.Data, typeof(T));

            if (result == null) return result;
            var propInfo = typeof(T).GetProperty(msg.AssociatedTotalCountPropertyName);
            if (propInfo != null)
                result.SetPropertyValue(msg.AssociatedTotalCountPropertyName, msg.Total);

            return result;
        }

        public static object GetOriginalData(this ResponseMessage msg, Type type)
        {
            object result = default;
            if (msg == null) return result;
            if ((msg.Data is string && type != typeof(string)) ||
                (msg.Data is Newtonsoft.Json.Linq.JObject) ||
                (msg.Data is Newtonsoft.Json.Linq.JArray && type != typeof(Newtonsoft.Json.Linq.JArray)))
            {
                result = msg.Data.ToString().JsonDeserialize(type, false);
            }
            else
                result = Convert.ChangeType(msg.Data, type);

            if (result == null) return result;
            var propInfo = type.GetProperty(msg.AssociatedTotalCountPropertyName);
            if (propInfo != null)
                result.SetPropertyValue(msg.AssociatedTotalCountPropertyName, msg.Total);

            return result;
        }
    }
}