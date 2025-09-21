using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OElite.Restme.MongoDb
{
    /// <summary>
    /// Helper class responsible for processing MongoDB queries with @ parameter substitution
    /// Converts @ references to actual property values from entities
    /// </summary>
    public static class QueryParameterSubstitutionHelper
    {
        private static readonly Regex ParameterRegex = new Regex(@"@(\w+)", RegexOptions.Compiled);

        /// <summary>
        /// Substitutes @ parameters in a MongoDB query with actual values from the entity
        /// </summary>
        /// <param name="query">MongoDB query string with @ parameters</param>
        /// <param name="entity">Entity containing the property values</param>
        /// <returns>MongoDB query string with substituted values</returns>
        public static string SubstituteParameters<T>(string query, T entity) where T : IEntity
        {
            if (string.IsNullOrWhiteSpace(query))
                return query;

            try
            {
                var entityType = typeof(T);
                var result = query;

                // Find all @ parameters in the query
                var matches = ParameterRegex.Matches(query);
                var substitutions = new Dictionary<string, string>();

                foreach (Match match in matches)
                {
                    var parameterName = match.Groups[1].Value;
                    var placeholder = match.Value; // @ParameterName

                    if (!substitutions.ContainsKey(placeholder))
                    {
                        var propertyValue = GetPropertyValue(entity, parameterName, entityType);
                        var mongoValue = ConvertToMongoValue(propertyValue);
                        substitutions[placeholder] = mongoValue;
                    }
                }

                // Perform substitutions
                foreach (var substitution in substitutions)
                {
                    result = result.Replace(substitution.Key, substitution.Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to substitute parameters in query: {query}", ex);
            }
        }

        /// <summary>
        /// Gets the value of a property from an entity using reflection
        /// </summary>
        private static object? GetPropertyValue<T>(T entity, string propertyName, Type entityType)
            where T : IEntity
        {
            var property = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found in entity type '{entityType.Name}'");
            }

            return property.GetValue(entity);
        }

        /// <summary>
        /// Converts a .NET value to its MongoDB JSON representation
        /// </summary>
        private static string ConvertToMongoValue(object? value)
        {
            if (value == null)
                return "null";

            return value switch
            {
                // String values need quotes
                string stringValue => $"\"{EscapeJsonString(stringValue)}\"",

                // Numeric types
                int or long or short or byte => value.ToString()!,
                float or double or decimal => value.ToString()!,

                // Boolean
                bool boolValue => boolValue.ToString().ToLower(),

                // DateTime to MongoDB ISODate
                DateTime dateTime => $"ISODate(\"{dateTime:yyyy-MM-ddTHH:mm:ss.fffZ}\")",
                DateTimeOffset dateTimeOffset => $"ISODate(\"{dateTimeOffset:yyyy-MM-ddTHH:mm:ss.fffZ}\")",

                // OElite DbObjectId (used to map into MongoDB ObjectId)
                DbObjectId objectId => $"ObjectId(\"{objectId}\")",

                // Guid
                Guid guid => $"\"{guid}\"",

                // Enum
                Enum enumValue => ((int)(object)enumValue).ToString(),

                // Arrays and collections
                global::System.Collections.IEnumerable enumerable when !(value is string) =>
                    ConvertCollectionToMongoArray(enumerable),

                // Default: convert to string and quote
                _ => $"\"{EscapeJsonString(value.ToString() ?? "")}\"",
            };
        }

        /// <summary>
        /// Converts a collection to MongoDB array syntax
        /// </summary>
        private static string ConvertCollectionToMongoArray(global::System.Collections.IEnumerable collection)
        {
            var items = new List<string>();

            foreach (var item in collection)
            {
                items.Add(ConvertToMongoValue(item));
            }

            return $"[{string.Join(", ", items)}]";
        }

        /// <summary>
        /// Escapes special characters in JSON strings
        /// </summary>
        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("\\", "\\\\") // Escape backslashes
                .Replace("\"", "\\\"") // Escape quotes
                .Replace("\n", "\\n") // Escape newlines
                .Replace("\r", "\\r") // Escape carriage returns
                .Replace("\t", "\\t"); // Escape tabs
        }

        /// <summary>
        /// Validates that a query string contains valid @ parameter references
        /// </summary>
        /// <param name="query">Query string to validate</param>
        /// <param name="entityType">Entity type to validate properties against</param>
        /// <returns>List of validation errors (empty if valid)</returns>
        public static List<string> ValidateParameterReferences(string query, Type entityType)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(query))
                return errors;

            var matches = ParameterRegex.Matches(query);

            foreach (Match match in matches)
            {
                var parameterName = match.Groups[1].Value;
                var property = entityType.GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    errors.Add(
                        $"Property '{parameterName}' referenced in query does not exist in entity type '{entityType.Name}'");
                }
            }

            return errors;
        }

        /// <summary>
        /// Extracts all @ parameter names from a query string
        /// </summary>
        /// <param name="query">Query string to analyze</param>
        /// <returns>List of parameter names (without the @ prefix)</returns>
        public static List<string> ExtractParameterNames(string query)
        {
            var parameters = new List<string>();

            if (string.IsNullOrWhiteSpace(query))
                return parameters;

            var matches = ParameterRegex.Matches(query);

            foreach (Match match in matches)
            {
                var parameterName = match.Groups[1].Value;
                if (!parameters.Contains(parameterName))
                {
                    parameters.Add(parameterName);
                }
            }

            return parameters;
        }
    }
}