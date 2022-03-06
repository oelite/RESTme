using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OElite
{
    public static class ObjectUtils
    {
        public static string JsonSerialize(this object objectValue, bool attemptResponseMessageConvertIfListType = true,
            JsonSerializerSettings serializerSettings = null)
        {
            if (!(objectValue is IList) || !attemptResponseMessageConvertIfListType)
                return StringUtils.JsonSerialize(objectValue,
                    serializerSettings);

            var responseMessage = new ResponseMessage(objectValue);
            return StringUtils.JsonSerialize(responseMessage, serializerSettings);
        }

        public static T CreateObject<T>()
        {
            return Activator.CreateInstance<T>();
        }

        public static T ParseEnum<T>(this int enumValue)
        {
            return (T) Enum.Parse(typeof(T), enumValue.ToString());
        }

        public static T ParseEnum<T>(this string enumValue)
        {
            return (T) Enum.Parse(typeof(T), enumValue);
        }

        public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsConstructedGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }

                toCheck = toCheck.DeclaringType;
            }

            return false;
        }

        public static Random RandomWithUniqueSeed()
        {
            return
                new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8),
                    System.Globalization.NumberStyles.HexNumber));
        }

        public static PropertyInfo GetPropertyInfo(Type type, string propertyName)
        {
            PropertyInfo propInfo = null;
            do
            {
                propInfo = type.GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.DeclaringType;
            } while (propInfo == null && type != null);

            return propInfo;
        }

        public static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            FieldInfo fieldInfo = null;
            do
            {
                fieldInfo = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.DeclaringType;
            } while (fieldInfo == null && type != null);

            return fieldInfo;
        }

        public static object GetPropertyValue(this object obj, string propertyName,
            bool throwExceptionIfNotExist = false)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            var objType = obj.GetType();
            var propInfo = GetPropertyInfo(objType, propertyName);
            if (propInfo == null && throwExceptionIfNotExist)
                throw new ArgumentOutOfRangeException(nameof(propertyName),
                    $"Couldn't find property {propertyName} in type {objType.FullName}");
            return propInfo == null ? null : propInfo.GetValue(obj, null);
        }

        public static object GetPropertyValueEnhanced(this object obj, string propName)
        {
            if (propName.IsNullOrEmpty() || obj == null)
                return null;

            var subObjIndex = propName.IndexOf(".", StringComparison.Ordinal);
            if (subObjIndex > 0)
            {
                var currentProp = propName.Substring(0, subObjIndex);
                var objValue = obj.GetPropertyValue(currentProp);
                return objValue != null
                    ? GetPropertyValueEnhanced(objValue, propName.Substring(subObjIndex + 1))
                    : null;
            }

            return obj.GetPropertyValue(propName);
        }


        public static object GetFieldValue(this object obj, string fieldName)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            var objType = obj.GetType();
            var fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null)
                throw new ArgumentOutOfRangeException("fieldName",
                    string.Format("Couldn't find field {0} in type {1}", fieldName, objType.FullName));
            return fieldInfo.GetValue(obj);
        }

        public static void SetPropertyValue(this object obj, string propertyName, object val,
            bool throwExceptionIfNotFound = true)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            var objType = obj.GetType();
            var propInfo = GetPropertyInfo(objType, propertyName);
            if (propInfo == null)
            {
                if (throwExceptionIfNotFound)
                    throw new ArgumentOutOfRangeException("propertyName",
                        string.Format("Couldn't find property {0} in type {1}", propertyName, objType.FullName));
                return;
            }

            try
            {
                propInfo.SetValue(obj, val, null);
            }
            catch
            {
                //scenario - for nullable enum  (note: for more scenarios, just add more code here
                if (propInfo.PropertyType.IsConstructedGenericType &&
                    Nullable.GetUnderlyingType(propInfo.PropertyType) != null)
                {
                    var enumType = Nullable.GetUnderlyingType(propInfo.PropertyType);
                    try
                    {
                        var enumValue = Enum.ToObject(enumType, val);
                        propInfo.SetValue(obj,
                            StringUtils.GetStringValueOrEmpty(val).IsNullOrEmpty() ? null : enumValue,
                            null);
                    }
                    catch
                    {
                        var enumValue = Enum.Parse(enumType, StringUtils.GetStringValueOrEmpty(val));
                        propInfo.SetValue(obj,
                            StringUtils.GetStringValueOrEmpty(val).IsNullOrEmpty() ? null : enumValue,
                            null);
                    }
                }
            }
        }

        public static void SetFieldValue(this object obj, string fieldName, object val,
            bool throwExceptionIfNotFound = true)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            var objType = obj.GetType();
            var fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null)
            {
                if (throwExceptionIfNotFound)
                    throw new ArgumentOutOfRangeException("propertyName",
                        string.Format("Couldn't find property {0} in type {1}", fieldName, objType.FullName));
                else
                    return;
            }

            try
            {
                fieldInfo.SetValue(obj, val);
            }
            catch
            {
                //scenario - for nullable enum  (note: for more scenarios, just add more code here
                if (fieldInfo.FieldType.IsConstructedGenericType &&
                    Nullable.GetUnderlyingType(fieldInfo.FieldType) != null)
                {
                    var enumType = Nullable.GetUnderlyingType(fieldInfo.FieldType);
                    try
                    {
                        var enumValue = Enum.ToObject(enumType, val);
                        fieldInfo.SetValue(obj,
                            StringUtils.GetStringValueOrEmpty(val).IsNullOrEmpty() ? null : enumValue);
                    }
                    catch
                    {
                        var enumValue = Enum.Parse(enumType, StringUtils.GetStringValueOrEmpty(val));
                        fieldInfo.SetValue(obj,
                            StringUtils.GetStringValueOrEmpty(val).IsNullOrEmpty() ? null : enumValue);
                    }
                }
            }
        }
    }
}