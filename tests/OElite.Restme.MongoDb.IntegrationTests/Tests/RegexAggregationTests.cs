using FluentAssertions;
using OElite;
using OElite.Restme.MongoDb.IntegrationTests.Infrastructure;
using OElite.Restme.MongoDb.IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.MongoDb.IntegrationTests.Tests;

/// <summary>
/// Tests MongoDB regex operations including $regexEscape in aggregation pipelines
/// </summary>
public class RegexAggregationTests : TestBase
{
    private readonly ITestOutputHelper _output;

    public RegexAggregationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Aggregation_ShouldUseRegexMatch_WhenMatchingSpecialCharacters()
    {
        // Arrange - Create products with special characters
        var products = new[]
        {
            new TestProduct { Name = "Product (Premium)", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product [Standard]", Price = 200.00m, IsActive = true },
            new TestProduct { Name = "Product {Deluxe}", Price = 300.00m, IsActive = true },
            new TestProduct { Name = "Product $pecial", Price = 400.00m, IsActive = true },
            new TestProduct { Name = "Product.Basic", Price = 50.00m, IsActive = true },
            new TestProduct { Name = "Product*Advanced", Price = 150.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Test: Use escaped regex to match products with parentheses (manual escaping)
        var pipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "name", new Dictionary<string, object>
                        {
                            { "$regex", @"\(Premium\)" }, // Manually escaped parentheses
                            { "$options", "i" }
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipeline);

        _output.WriteLine($"Found {results.Count} products matching escaped regex pattern: \\(Premium\\)");

        foreach (var result in results)
        {
            _output.WriteLine($"Product: {result["name"]}, Price: {result["price"]}");
        }

        // Should find exactly 1 product with "(Premium)" in the name
        results.Should().HaveCount(1, "because only one product contains '(Premium)' when using escaped regex");

        var foundProduct = results[0];
        foundProduct["name"].ToString().Should().Contain("(Premium)");
    }

    [Fact]
    public async Task Aggregation_ShouldUseRegexMatch_WhenMatchingPatterns()
    {
        // Arrange - Create products with various patterns
        var products = new[]
        {
            new TestProduct { Name = "ABC123", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "XYZ456", Price = 200.00m, IsActive = true },
            new TestProduct { Name = "Product-ABC", Price = 300.00m, IsActive = true },
            new TestProduct { Name = "123-Product", Price = 400.00m, IsActive = true },
            new TestProduct { Name = "NoNumbers", Price = 50.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Test basic regex pattern matching with $match and $regex
        var pipelineWithNumbers = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "name", new Dictionary<string, object>
                        {
                            { "$regex", @"\d+" }  // Contains numbers
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 }
                }
            }}
        };

        var pipelineWithThreeLetters = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "name", new Dictionary<string, object>
                        {
                            { "$regex", "^[A-Z]{3}" }  // Starts with 3 uppercase letters
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "price", 1 }
                }
            }}
        };

        var resultsWithNumbers = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipelineWithNumbers);

        var resultsWithThreeLetters = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipelineWithThreeLetters);

        _output.WriteLine($"Products with numbers: {resultsWithNumbers.Count}");
        foreach (var result in resultsWithNumbers)
        {
            _output.WriteLine($"  - {result["name"]} (Price: {result["price"]})");
        }

        _output.WriteLine($"Products starting with 3 uppercase letters: {resultsWithThreeLetters.Count}");
        foreach (var result in resultsWithThreeLetters)
        {
            _output.WriteLine($"  - {result["name"]} (Price: {result["price"]})");
        }

        // Verify regex matching results
        resultsWithNumbers.Should().HaveCount(3, "because ABC123, XYZ456, and 123-Product contain digits");

        var numbersNames = resultsWithNumbers.Select(r => r["name"].ToString()).ToList();
        numbersNames.Should().Contain("ABC123");
        numbersNames.Should().Contain("XYZ456");
        numbersNames.Should().NotContain("Product-ABC"); // "ABC" doesn't contain digits
        numbersNames.Should().Contain("123-Product");
        numbersNames.Should().NotContain("NoNumbers");

        // Should have ABC123 and XYZ456 (start with exactly 3 uppercase letters)
        resultsWithThreeLetters.Should().HaveCount(2, "because only ABC123 and XYZ456 start with 3 uppercase letters");

        var lettersNames = resultsWithThreeLetters.Select(r => r["name"].ToString()).ToList();
        lettersNames.Should().Contain("ABC123");
        lettersNames.Should().Contain("XYZ456");
        lettersNames.Should().NotContain("Product-ABC"); // Starts with "Product"
        lettersNames.Should().NotContain("123-Product"); // Starts with numbers
        lettersNames.Should().NotContain("NoNumbers");   // Doesn't match pattern
    }

    [Fact]
    public async Task Aggregation_ShouldUseRegexMatch_WhenCheckingEmailPatterns()
    {
        // Arrange - Create products with email-like patterns in descriptions
        var products = new[]
        {
            new TestProduct
            {
                Name = "Product A",
                Description = "Contact: support@company.com for help",
                Price = 100.00m,
                IsActive = true
            },
            new TestProduct
            {
                Name = "Product B",
                Description = "Email us at sales@business.org",
                Price = 200.00m,
                IsActive = true
            },
            new TestProduct
            {
                Name = "Product C",
                Description = "No email in this description",
                Price = 300.00m,
                IsActive = true
            }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Use basic regex matching to find products with email patterns
        var pipeline = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "description", new Dictionary<string, object>
                        {
                            { "$regex", @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}" }
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object>
                {
                    { "name", 1 },
                    { "description", 1 },
                    { "price", 1 }
                }
            }}
        };

        var results = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipeline);

        _output.WriteLine($"Products with email patterns: {results.Count}");

        foreach (var result in results)
        {
            _output.WriteLine($"Product: {result["name"]}, Description: {result["description"]}");
        }

        // Verify email pattern matching
        results.Should().HaveCount(2, "because two products have email patterns in their descriptions");

        var productNames = results.Select(r => r["name"].ToString()).ToList();
        productNames.Should().Contain("Product A");
        productNames.Should().Contain("Product B");
        productNames.Should().NotContain("Product C");
    }

    [Fact]
    public async Task Aggregation_ShouldShowRegexDifference_WhenSearchingUserInput()
    {
        // Arrange - Simulate searching for user input that contains special regex characters
        var products = new[]
        {
            new TestProduct { Name = "Product v1.0", Price = 100.00m, IsActive = true },
            new TestProduct { Name = "Product v2.0", Price = 200.00m, IsActive = true },
            new TestProduct { Name = "Product v10.5", Price = 300.00m, IsActive = true },
            new TestProduct { Name = "Product v1x0", Price = 400.00m, IsActive = true }, // Should not match "v1.0" literally
            new TestProduct { Name = "Another Product", Price = 50.00m, IsActive = true }
        };

        var query = DbCentre.GetQuery<TestProduct>();
        await query.InsertManyAsync(products);

        // Test 1: Without escaping (incorrect - will match "v1x0" too because . matches any character)
        var pipelineUnescaped = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "name", new Dictionary<string, object>
                        {
                            { "$regex", "v1.0" },  // Unescaped dot
                            { "$options", "i" }
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object> { { "name", 1 }, { "price", 1 } } }}
        };

        var unescapedResults = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipelineUnescaped);

        // Test 2: With manual escaping (correct - will match only literal "v1.0")
        var pipelineEscaped = new Dictionary<string, object>[]
        {
            new() { { "$match", new Dictionary<string, object>
                {
                    { "name", new Dictionary<string, object>
                        {
                            { "$regex", @"v1\.0" },  // Manually escaped dot
                            { "$options", "i" }
                        }
                    }
                }
            }},
            new() { { "$project", new Dictionary<string, object> { { "name", 1 }, { "price", 1 } } }}
        };

        var escapedResults = await DbCentre.GetQuery<TestProduct>()
            .AggregateAsync<Dictionary<string, object>>(pipelineEscaped);

        _output.WriteLine($"Unescaped search for 'v1.0' found {unescapedResults.Count} products:");
        foreach (var result in unescapedResults)
        {
            _output.WriteLine($"  - {result["name"]}");
        }

        _output.WriteLine($"Escaped search for 'v1\\.0' found {escapedResults.Count} products:");
        foreach (var result in escapedResults)
        {
            _output.WriteLine($"  - {result["name"]}");
        }

        // Without escaping, should match both "v1.0" and "v1x0" (because . matches any character)
        unescapedResults.Should().HaveCount(2, "because unescaped '.' matches any character");
        var unescapedNames = unescapedResults.Select(r => r["name"].ToString()).ToList();
        unescapedNames.Should().Contain("Product v1.0");
        unescapedNames.Should().Contain("Product v1x0");

        // With escaping, should match only "v1.0" literally
        escapedResults.Should().HaveCount(1, "because escaped '\\.' matches only literal dot");
        escapedResults[0]["name"].ToString().Should().Be("Product v1.0");
    }
}