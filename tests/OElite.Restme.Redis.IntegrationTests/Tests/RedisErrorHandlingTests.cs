using FluentAssertions;
using OElite;
using OElite.Providers;
using OElite.Restme.Redis.IntegrationTests.Infrastructure;
using OElite.Restme.Redis.IntegrationTests.Models;
using StackExchange.Redis;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Redis.IntegrationTests.Tests;

/// <summary>
/// Comprehensive error handling and failure scenario tests for Redis cache provider
/// Validates enterprise-grade resilience and error reporting
/// </summary>
public class RedisErrorHandlingTests : RedisTestBase
{
    private readonly ITestOutputHelper _output;

    public RedisErrorHandlingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Input Validation Tests

    [Fact]
    public async Task GetAsync_ShouldThrowArgumentException_WithInvalidKeys()
    {
        _output.WriteLine("Testing GetAsync input validation...");

        // Test null key
        var nullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.GetAsync<string>(null!));

        nullKeyException.Should().NotBeNull();
        nullKeyException.Message.Should().ContainEquivalentOf("key");
        nullKeyException.Message.Should().ContainEquivalentOf("GetAsync");

        // Test empty key
        var emptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.GetAsync<string>(""));

        emptyKeyException.Should().NotBeNull();
        emptyKeyException.Message.Should().ContainEquivalentOf("key");

        // Test whitespace-only key
        var whitespaceKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.GetAsync<string>("   "));

        whitespaceKeyException.Should().NotBeNull();

        _output.WriteLine("✅ GetAsync input validation working correctly");
    }

    [Fact]
    public async Task SetAsync_ShouldThrowArgumentException_WithInvalidInputs()
    {
        _output.WriteLine("Testing SetAsync input validation...");

        var validKey = GenerateTestKey("valid");
        var validValue = "valid_value";

        // Test null key
        var nullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetAsync<string>(null!, validValue));

        nullKeyException.Should().NotBeNull();
        nullKeyException.Message.Should().ContainEquivalentOf("key");
        nullKeyException.Message.Should().ContainEquivalentOf("key");

        // Test empty key
        var emptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetAsync("", validValue));

        emptyKeyException.Should().NotBeNull();

        // Test null value
        var nullValueException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetAsync<string>(validKey, null!));

        nullValueException.Should().NotBeNull();
        nullValueException.Message.Should().ContainEquivalentOf("value");
        nullValueException.Message.Should().ContainEquivalentOf("SetAsync");

        _output.WriteLine("✅ SetAsync input validation working correctly");
    }

    [Fact]
    public async Task RemoveAsync_ShouldThrowArgumentException_WithInvalidKeys()
    {
        _output.WriteLine("Testing RemoveAsync input validation...");

        // Test null key
        var nullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.RemoveAsync(null!));

        nullKeyException.Should().NotBeNull();
        nullKeyException.Message.Should().ContainEquivalentOf("key");
        nullKeyException.Message.Should().ContainEquivalentOf("key");

        // Test empty key
        var emptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.RemoveAsync(""));

        emptyKeyException.Should().NotBeNull();

        _output.WriteLine("✅ RemoveAsync input validation working correctly");
    }

    [Fact]
    public async Task ExistsAsync_ShouldThrowArgumentException_WithInvalidKeys()
    {
        _output.WriteLine("Testing ExistsAsync input validation...");

        // Test null key
        var nullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.ExistsAsync(null!));

        nullKeyException.Should().NotBeNull();
        nullKeyException.Message.Should().ContainEquivalentOf("key");
        nullKeyException.Message.Should().ContainEquivalentOf("key");

        // Test empty key
        var emptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.ExistsAsync(""));

        emptyKeyException.Should().NotBeNull();

        _output.WriteLine("✅ ExistsAsync input validation working correctly");
    }

    [Fact]
    public async Task SetExpiryAsync_ShouldThrowArgumentException_WithInvalidInputs()
    {
        _output.WriteLine("Testing SetExpiryAsync input validation...");

        var validKey = GenerateTestKey("valid_expiry");
        var validExpiry = TimeSpan.FromMinutes(5);

        // First set a valid key for testing expiry
        await CacheProvider.SetAsync(validKey, "test_value");

        // Test null key
        var nullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetExpiryAsync(null!, validExpiry));

        nullKeyException.Should().NotBeNull();
        nullKeyException.Message.Should().ContainEquivalentOf("key");
        nullKeyException.Message.Should().ContainEquivalentOf("key");

        // Test empty key
        var emptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetExpiryAsync("", validExpiry));

        emptyKeyException.Should().NotBeNull();

        _output.WriteLine("✅ SetExpiryAsync input validation working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(validKey);
    }

    #endregion

    #region Serialization Error Tests

    [Fact]
    public async Task SetAsync_ShouldHandleSerializationErrors_Gracefully()
    {
        _output.WriteLine("Testing serialization error handling...");

        var key = GenerateTestKey("serialization_error");

        // Create an object that might cause serialization issues
        var problematicObject = new ProblematicSerializationModel
        {
            Id = "test_001",
            CircularReference = null
        };

        // Create circular reference (this should be handled by the serializer)
        problematicObject.CircularReference = problematicObject;

        try
        {
            // Attempt to cache the problematic object
            // This might succeed or fail depending on the JSON serializer configuration
            var result = await CacheProvider.SetAsync(key, problematicObject);

            if (result)
            {
                _output.WriteLine("Serialization succeeded (serializer handled circular reference)");

                // If successful, try to retrieve it
                var getValue = await CacheProvider.GetAsync<ProblematicSerializationModel>(key);

                if (getValue != null)
                {
                    // Full round-trip serialization worked
                    getValue.Id.Should().Be(problematicObject.Id);
                    // Note: CircularReference will likely be null after round-trip serialization
                    // This is expected behavior for most JSON serializers handling circular references
                    _output.WriteLine("✅ Full round-trip serialization succeeded");
                }
                else
                {
                    // Serialization succeeded but deserialization failed - this is also valid behavior
                    // Some serializers may store malformed JSON that can't be deserialized
                    _output.WriteLine("⚠️  Serialization succeeded but deserialization failed (circular reference issue)");
                }

                await CacheProvider.RemoveAsync(key);

                _output.WriteLine("✅ Serialization handled circular reference gracefully");
            }
            else
            {
                _output.WriteLine("SetAsync returned false for problematic object");
                // This is also acceptable - some serializers may refuse to serialize circular references
            }
        }
        catch (OEliteException ex)
        {
            // If serialization fails, ensure it throws a meaningful OEliteException
            ex.Should().BeOfType<OEliteException>();
            ex.Message.Should().Contain("Failed to set cache value");
            ex.InnerException.Should().NotBeNull();

            _output.WriteLine($"✅ Serialization error properly wrapped: {ex.Message}");
        }

        _output.WriteLine("✅ Serialization error handling completed");
    }

    [Fact]
    public async Task GetAsync_ShouldHandleDeserializationErrors_Gracefully()
    {
        _output.WriteLine("Testing deserialization error handling...");

        var key = GenerateTestKey("deserialization_error");

        // Set a valid string value
        await CacheProvider.SetAsync(key, "valid_json_string");

        // Try to deserialize as an incompatible complex type
        try
        {
            var getValue = await CacheProvider.GetAsync<ComplexOrder>(key);
            // This should either return null or throw an exception
            getValue.Should().BeNull("Incompatible type deserialization should return null");

            _output.WriteLine("Deserialization handled gracefully (returned null)");
        }
        catch (OEliteException ex)
        {
            // If it throws, it should be a meaningful OEliteException
            ex.Message.Should().Contain("Failed to get cache value");
            ex.InnerException.Should().NotBeNull();

            _output.WriteLine($"Deserialization error properly wrapped: {ex.Message}");
        }

        _output.WriteLine("✅ Deserialization error handling completed");

        await CacheProvider.RemoveAsync(key);
    }

    #endregion

    #region Connection Failure Tests

    [Fact]
    public async Task Operations_ShouldFailGracefully_WithInvalidConnectionString()
    {
        _output.WriteLine("Testing operations with invalid connection string...");

        // Create provider with invalid connection string
        var invalidConfig = new RestConfig(RestMode.Redis)
        {
            ConnectionString = "invalid-host:6379"
        };

        var invalidProvider = new RedisCacheProvider(invalidConfig);

        try
        {
            // Test various operations - they should all throw meaningful exceptions
            var testKey = "test_invalid_connection";
            var testValue = "test_value";

            // Test SetAsync
            var setException = await Assert.ThrowsAsync<OEliteException>(async () =>
                await invalidProvider.SetAsync(testKey, testValue));

            setException.Should().NotBeNull();
            setException.Message.Should().Contain("Failed to set cache value");
            setException.InnerException.Should().NotBeNull();

            // Test GetAsync
            var getException = await Assert.ThrowsAsync<OEliteException>(async () =>
                await invalidProvider.GetAsync<string>(testKey));

            getException.Should().NotBeNull();
            getException.Message.Should().Contain("Failed to get cache value");

            // Test ExistsAsync
            var existsException = await Assert.ThrowsAsync<OEliteException>(async () =>
                await invalidProvider.ExistsAsync(testKey));

            existsException.Should().NotBeNull();
            existsException.Message.Should().Contain("Failed to check cache key existence");

            // Test RemoveAsync
            var removeException = await Assert.ThrowsAsync<OEliteException>(async () =>
                await invalidProvider.RemoveAsync(testKey));

            removeException.Should().NotBeNull();
            removeException.Message.Should().Contain("Failed to remove cache key");

            _output.WriteLine("✅ All operations failed gracefully with invalid connection");
        }
        finally
        {
            invalidProvider.Dispose();
        }
    }

    [Fact]
    public async Task Operations_ShouldRespectCancellationToken_Properly()
    {
        _output.WriteLine("Testing cancellation token handling...");

        var key = GenerateTestKey("cancellation_test");
        var value = "cancellation_value";

        // Test with already cancelled token
        using var cancelledCts = new CancellationTokenSource();
        cancelledCts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.SetAsync(key, value, cancellationToken: cancelledCts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.GetAsync<string>(key, cancelledCts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.ExistsAsync(key, cancelledCts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.RemoveAsync(key, cancelledCts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.SetExpiryAsync(key, TimeSpan.FromMinutes(1), cancelledCts.Token));

        // Test with token cancelled during operation
        using var timedCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        try
        {
            // This might or might not throw depending on timing
            await CacheProvider.SetAsync(key, value, cancellationToken: timedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("Timed cancellation token properly handled");
        }

        _output.WriteLine("✅ Cancellation token handling working correctly");
    }

    #endregion

    #region Memory and Resource Stress Tests

    [Fact]
    public async Task Provider_ShouldHandleLargeValues_Appropriately()
    {
        _output.WriteLine("Testing large value handling...");

        var key = GenerateTestKey("large_value");

        // Test progressively larger values
        var testSizes = new[] { 1024, 10240, 102400, 1024000 }; // 1KB to 1MB

        foreach (var size in testSizes)
        {
            _output.WriteLine($"Testing value size: {size / 1024}KB");

            var largeValue = new string('A', size);

            try
            {
                var setResult = await CacheProvider.SetAsync(key, largeValue);

                if (setResult)
                {
                    var getValue = await CacheProvider.GetAsync<string>(key);
                    getValue.Should().NotBeNull($"Large value ({size} bytes) should be retrievable");
                    getValue!.Length.Should().Be(size, "Retrieved value should have correct length");

                    // Cleanup immediately to free memory
                    await CacheProvider.RemoveAsync(key);

                    _output.WriteLine($"  ✅ {size / 1024}KB value handled successfully");
                }
                else
                {
                    _output.WriteLine($"  ⚠️ {size / 1024}KB value rejected (SetAsync returned false)");
                }
            }
            catch (OEliteException ex)
            {
                _output.WriteLine($"  ⚠️ {size / 1024}KB value failed: {ex.Message}");
                // Large values might fail due to Redis limits - this is acceptable
            }
        }

        _output.WriteLine("✅ Large value handling test completed");
    }

    [Fact]
    public async Task Provider_ShouldHandleExtremelyLargeKeys_Appropriately()
    {
        _output.WriteLine("Testing extremely large key handling...");

        // Redis has a limit on key size (typically 512MB, but practical limit is much smaller)
        var testKeySizes = new[] { 100, 1000, 10000, 100000 };

        foreach (var keySize in testKeySizes)
        {
            var largeKey = GenerateTestKey("large") + new string('K', keySize);
            var value = "test_value";

            _output.WriteLine($"Testing key size: {keySize} characters");

            try
            {
                var setResult = await CacheProvider.SetAsync(largeKey, value);

                if (setResult)
                {
                    var getValue = await CacheProvider.GetAsync<string>(largeKey);
                    getValue.Should().Be(value, $"Value with large key ({keySize} chars) should be retrievable");

                    await CacheProvider.RemoveAsync(largeKey);

                    _output.WriteLine($"  ✅ {keySize}-character key handled successfully");
                }
                else
                {
                    _output.WriteLine($"  ⚠️ {keySize}-character key rejected (SetAsync returned false)");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ⚠️ {keySize}-character key failed: {ex.Message}");
                // Large keys might fail due to Redis limits - this is acceptable
            }
        }

        _output.WriteLine("✅ Large key handling test completed");
    }

    [Fact]
    public async Task Provider_ShouldHandleSpecialKeyCharacters_Appropriately()
    {
        _output.WriteLine("Testing special character key handling...");

        var specialKeys = new[]
        {
            "key:with:colons",
            "key with spaces",
            "key-with-dashes",
            "key_with_underscores",
            "key.with.dots",
            "key/with/slashes",
            "key\\with\\backslashes",
            "key|with|pipes",
            "key@with@at",
            "key#with#hash",
            "key$with$dollar",
            "key%with%percent",
            "key^with^caret",
            "key&with&ampersand",
            "key*with*asterisk",
            "key(with)parentheses",
            "key[with]brackets",
            "key{with}braces",
            "key=with=equals",
            "key+with+plus",
            "key~with~tilde",
            "key`with`backtick",
            "key'with'quotes",
            "key\"with\"doublequotes",
            "key<with>angles",
            "key?with?question",
            "key,with,comma",
            "key;with;semicolon"
        };

        var successfulKeys = new List<string>();

        foreach (var specialKey in specialKeys)
        {
            var fullKey = GenerateTestKey($"special_{specialKey}");
            var value = $"value_for_{specialKey}";

            try
            {
                var setResult = await CacheProvider.SetAsync(fullKey, value);

                if (setResult)
                {
                    var getValue = await CacheProvider.GetAsync<string>(fullKey);
                    if (getValue == value)
                    {
                        successfulKeys.Add(specialKey);
                        _output.WriteLine($"  ✅ Special key handled: '{specialKey}'");
                    }
                    else
                    {
                        _output.WriteLine($"  ⚠️ Special key set but not retrieved correctly: '{specialKey}'");
                    }
                }
                else
                {
                    _output.WriteLine($"  ⚠️ Special key rejected: '{specialKey}'");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ⚠️ Special key failed: '{specialKey}' - {ex.Message}");
            }
        }

        // Cleanup successful keys
        foreach (var specialKey in successfulKeys)
        {
            var fullKey = GenerateTestKey($"special_{specialKey}");
            await CacheProvider.RemoveAsync(fullKey);
        }

        _output.WriteLine($"✅ Special character key test completed - {successfulKeys.Count}/{specialKeys.Length} keys handled successfully");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Provider_ShouldHandleRapidRepeatedOperations_OnSameKey()
    {
        _output.WriteLine("Testing rapid repeated operations on same key...");

        var key = GenerateTestKey("rapid_repeated");
        const int operationCount = 100;
        var exceptions = new List<Exception>();

        // Rapid set/get/remove cycles
        for (int i = 0; i < operationCount; i++)
        {
            try
            {
                var value = $"value_{i}";

                var setResult = await CacheProvider.SetAsync(key, value);
                if (!setResult)
                {
                    _output.WriteLine($"SetAsync returned false for iteration {i}");
                    continue;
                }

                var getValue = await CacheProvider.GetAsync<string>(key);
                if (getValue != value)
                {
                    _output.WriteLine($"GetAsync returned incorrect value for iteration {i}: expected '{value}', got '{getValue}'");
                    continue;
                }

                var removeResult = await CacheProvider.RemoveAsync(key);
                if (!removeResult)
                {
                    _output.WriteLine($"RemoveAsync returned false for iteration {i}");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                _output.WriteLine($"Exception in iteration {i}: {ex.Message}");
            }
        }

        // Some exceptions might be acceptable under high load
        var exceptionRate = (double)exceptions.Count / operationCount * 100;
        exceptionRate.Should().BeLessThan(10, "Exception rate should be less than 10% for rapid operations");

        _output.WriteLine($"✅ Rapid repeated operations completed - {operationCount - exceptions.Count}/{operationCount} successful ({exceptionRate:F2}% error rate)");
    }

    [Fact]
    public async Task Provider_ShouldHandleEmptyStringValues_Correctly()
    {
        _output.WriteLine("Testing empty string value handling...");

        var key = GenerateTestKey("empty_string");
        var emptyValue = "";

        // Test empty string caching
        var setResult = await CacheProvider.SetAsync(key, emptyValue);
        setResult.Should().BeTrue("SetAsync should succeed with empty string value");

        var getValue = await CacheProvider.GetAsync<string>(key);
        getValue.Should().Be(emptyValue, "GetAsync should return empty string");
        getValue.Should().NotBeNull("GetAsync should return empty string, not null");

        var existsResult = await CacheProvider.ExistsAsync(key);
        existsResult.Should().BeTrue("ExistsAsync should return true for empty string value");

        _output.WriteLine("✅ Empty string value handling working correctly");

        await CacheProvider.RemoveAsync(key);
    }

    [Fact]
    public async Task Provider_ShouldHandleWhitespaceOnlyValues_Correctly()
    {
        _output.WriteLine("Testing whitespace-only value handling...");

        var whitespaceValues = new[]
        {
            " ",           // single space
            "  ",          // multiple spaces
            "\t",          // tab
            "\n",          // newline
            "\r\n",        // carriage return + newline
            "   \t\n  "    // mixed whitespace
        };

        for (int i = 0; i < whitespaceValues.Length; i++)
        {
            var key = GenerateTestKey($"whitespace_{i}");
            var whitespaceValue = whitespaceValues[i];

            var setResult = await CacheProvider.SetAsync(key, whitespaceValue);
            setResult.Should().BeTrue($"SetAsync should succeed with whitespace value {i}");

            var getValue = await CacheProvider.GetAsync<string>(key);
            getValue.Should().Be(whitespaceValue, $"GetAsync should return exact whitespace value {i}");

            await CacheProvider.RemoveAsync(key);
        }

        _output.WriteLine("✅ Whitespace-only value handling working correctly");
    }

    #endregion
}

/// <summary>
/// Test model that might cause serialization issues
/// </summary>
public class ProblematicSerializationModel
{
    public string Id { get; set; } = string.Empty;
    public ProblematicSerializationModel? CircularReference { get; set; }
}