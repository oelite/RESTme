using System;

namespace OElite.Restme.MongoDb
{
    /// <summary>
    /// Attribute to mark a property as a denormalized collection from another collection
    /// Supports both property-based filtering and advanced query-based denormalization
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DenormalizedCollectionAttribute : DenormalizedAttribute
    {
        /// <summary>
        /// Unified constructor for collection denormalization
        /// </summary>
        /// <param name="fromCollection">The source collection name</param>
        /// <param name="referenceKey">Enhanced reference key using @ for current class properties or # for current property class properties</param>
        /// <param name="collectionReference">If the mapped collection in fromCollection are sourced multiple times in referencing record, a collection reference key is required</param>
        /// <param name="query">MongoDB query string with @ parameter substitution</param>
        /// <param name="sort">MongoDB sort specification</param>
        /// <param name="limit">Maximum number of records to return</param>
        public DenormalizedCollectionAttribute(string fromCollection, string referenceKey = "#Id",
            string? collectionReference = null, string? query = null, string? sort = null, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(fromCollection))
            {
                throw new ArgumentException("FromCollection is required", nameof(fromCollection));
            }

            if (string.IsNullOrWhiteSpace(referenceKey) && string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException(
                    "Either ReferenceKey or Query is required for collection denormalization",
                    nameof(referenceKey));
            }

            // Initialize base class properties
            FromCollection = fromCollection;
            ReferenceKey = referenceKey;
            CollectionReference = collectionReference;
            
            // Create DbSimpleQuery if query is provided
            if (!string.IsNullOrWhiteSpace(query))
            {
                Query = new DbSimpleQuery(query, sort, limit);
            }
        }
    }
}