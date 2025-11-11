namespace OElite;

/// <summary>
/// Represents a simple MongoDB query configuration for denormalized field queries
/// Used to eliminate constructor ambiguity in DenormalizedFieldAttribute
/// </summary>
public class DbSimpleQuery
{
    /// <summary>
    /// MongoDB query with @ parameter substitution.
    /// Use @ to reference current entity properties (e.g., @CategoryId, @Status).
    /// Supports flexible JSON syntax with single quotes and multiline strings.
    ///
    /// Examples:
    /// - Simple: "{ '_id': @ProductId }"
    /// - Complex: "{ '_id': @CategoryId, 'status': @Status, 'price': { '$gte': @MinPrice } }"
    /// - Multiline: @"{
    ///     '_id': @CategoryId,
    ///     'status': { '$in': [10, 20] },
    ///     'createdOnUtc': { '$gt': @CreatedDate }
    /// }"
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// MongoDB sort specification for ordering the results.
    /// Supports flexible JSON syntax with single quotes and @ parameter substitution.
    ///
    /// Examples:
    /// - Simple: "{ 'createdOnUtc': -1 }" (descending by creation date)
    /// - Multiple fields: "{ 'priority': -1, 'name': 1 }" (priority desc, name asc)
    /// - With @ parameters: "{ '@SortField': @SortDirection }" (dynamic sorting)
    /// - Complex: "{ 'status': 1, 'updatedOnUtc': -1, 'name': 1 }"
    ///
    /// Sort values: 1 = ascending, -1 = descending
    /// If not specified, results are returned in natural MongoDB order.
    /// </summary>
    public string? Sort { get; set; }

    /// <summary>
    /// Maximum number of records to return from the query.
    /// - Positive number: Limit to that many records (default: 1 for fields)
    /// - -1: Return all qualified records (no limit)
    /// - 0: Return no records (useful for validation queries)
    /// </summary>
    public int Limit { get; set; } = 1;

    /// <summary>
    /// Constructor for creating a simple query
    /// </summary>
    /// <param name="query">MongoDB query string</param>
    /// <param name="sort">Optional sort specification</param>
    /// <param name="limit">Record limit (default: 1)</param>
    public DbSimpleQuery(string query, string? sort = null, int limit = 1)
    {
        Query = query;
        Sort = sort;
        Limit = limit;
    }
}