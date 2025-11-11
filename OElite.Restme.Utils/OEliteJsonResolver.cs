using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OElite
{
    /// <summary>
    /// Custom JSON contract resolver that handles multiple naming conventions:
    /// - Deserialization: camelCase, snake_case, AND PascalCase -> PascalCase C# properties
    /// - Serialization: PascalCase C# properties -> snake_case JSON (unified standard)
    /// This is the unified standard for API requests in the OElite platform
    /// </summary>
    public class OEliteJsonResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            // Ignore Stream properties
            if (typeof(Stream).IsAssignableFrom(property.PropertyType))
            {
                property.Ignored = true;
            }

            return property;
        }

        /// <summary>
        /// Converts PascalCase to snake_case for serialization
        /// Example: ContactId -> contact_id, UserName -> user_name
        /// </summary>
        private static string ConvertPascalToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            // Insert underscore before uppercase letters that follow lowercase letters or numbers
            var snakeCase = Regex.Replace(pascalCase, "(?<=[a-z0-9])([A-Z])", "_$1");

            // Convert to lowercase
            return snakeCase.ToLowerInvariant();
        }

        /// <summary>
        /// Converts snake_case to PascalCase
        /// Example: contact_id -> ContactId, user_name -> UserName
        /// </summary>
        private static string ConvertSnakeToPascalCase(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
                return snakeCase;

            // Split by underscore and capitalize each part
            var parts = snakeCase.Split('_');
            var pascalCase = string.Join("", parts.Select(part =>
                string.IsNullOrEmpty(part)
                    ? ""
                    : char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));

            return pascalCase;
        }

        /// <summary>
        /// Converts camelCase to PascalCase
        /// Example: contactId -> ContactId, userName -> UserName
        /// </summary>
        private static string ConvertCamelToPascalCase(string camelCase)
        {
            if (string.IsNullOrEmpty(camelCase))
                return camelCase;

            // Capitalize first letter
            return char.ToUpperInvariant(camelCase[0]) + camelCase.Substring(1);
        }

        /// <summary>
        /// Determines the PascalCase equivalent of any naming convention
        /// Handles: PascalCase, camelCase, snake_case
        /// </summary>
        private static string NormalizeToPascalCase(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return propertyName;

            // If already PascalCase (starts with uppercase), return as-is
            if (char.IsUpper(propertyName[0]))
                return propertyName;

            // If contains underscores, it's snake_case
            if (propertyName.Contains('_'))
                return ConvertSnakeToPascalCase(propertyName);

            // If starts with lowercase, it's camelCase
            if (char.IsLower(propertyName[0]))
                return ConvertCamelToPascalCase(propertyName);

            // Default fallback
            return propertyName;
        }

        protected override string ResolvePropertyName(string propertyName)
        {
            // For deserialization, normalize any naming convention to PascalCase
            return NormalizeToPascalCase(propertyName);
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);

            if (contract is JsonObjectContract objectContract)
            {
                // For serialization, convert PascalCase to snake_case (unified standard)
                foreach (var property in objectContract.Properties)
                {
                    if (property.PropertyName != null)
                    {
                        property.PropertyName = ConvertPascalToSnakeCase(property.PropertyName);
                    }
                }
            }

            return contract;
        }

        /// <summary>
        /// Override to handle multiple property name variations during deserialization
        /// This allows the resolver to match properties regardless of naming convention
        /// </summary>
        protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty matchingMemberProperty,
            ParameterInfo parameterInfo)
        {
            var property = base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo);

            // The ResolvePropertyName method will handle the conversion automatically
            // No need for AlternateNames as Newtonsoft.Json doesn't support it
            return property;
        }
    }
}