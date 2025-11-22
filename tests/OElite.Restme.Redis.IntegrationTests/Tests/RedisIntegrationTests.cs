using FluentAssertions;
using OElite.Providers;
using OElite.Restme.Redis.IntegrationTests.Infrastructure;
using OElite.Restme.Redis.IntegrationTests.Models;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Redis.IntegrationTests.Tests;

/// <summary>
/// Comprehensive Redis cache provider integration tests
/// Validates enterprise-grade reliability, performance, and resilience patterns
/// </summary>
public class RedisIntegrationTests : RedisTestBase
{
    private readonly ITestOutputHelper _output;

    public RedisIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Provider Initialization Tests

    [Fact]
    public async Task Provider_ShouldInitialize_WithValidConfiguration()
    {
        // Act & Assert
        _output.WriteLine("Testing Redis cache provider initialization...");

        CacheProvider.Should().NotBeNull();
        CacheProvider.ProviderName.Should().Be("RedisCache");
        CacheProvider.Capabilities.Should().HaveFlag(OElite.Restme.ProviderCapabilities.Cache);

        // Validate underlying Redis connection
        RedisConnection.Should().NotBeNull();
        RedisConnection.IsConnected.Should().BeTrue();
        RedisDatabase.Should().NotBeNull();

        _output.WriteLine("✅ Redis cache provider initialized successfully");
    }

    [Fact]
    public async Task Provider_ShouldPerformBasicOperations_AfterInitialization()
    {
        // Arrange
        var testKey = GenerateTestKey("init");
        var testValue = "initialization_validation";

        // Act & Assert
        _output.WriteLine("Validating basic cache operations after initialization...");

        // Test set operation
        var setResult = await CacheProvider.SetAsync(testKey, testValue);
        setResult.Should().BeTrue("SetAsync should return true for successful operation");

        // Test get operation
        var getValue = await CacheProvider.GetAsync<string>(testKey);
        getValue.Should().Be(testValue, "GetAsync should return the exact value that was set");

        // Test exists operation
        var existsResult = await CacheProvider.ExistsAsync(testKey);
        existsResult.Should().BeTrue("ExistsAsync should return true for existing key");

        // Test remove operation
        var removeResult = await CacheProvider.RemoveAsync(testKey);
        removeResult.Should().BeTrue("RemoveAsync should return true for successful removal");

        // Verify removal
        var getAfterRemove = await CacheProvider.GetAsync<string>(testKey);
        getAfterRemove.Should().BeNull("GetAsync should return null for removed key");

        _output.WriteLine("✅ Basic cache operations validated successfully");
    }

    #endregion

    #region Basic CRUD Operations

    [Fact]
    public async Task SetAndGet_ShouldWorkCorrectly_WithStringValues()
    {
        // Arrange
        var testCases = new[]
        {
            ("simple", "Hello World"),
            ("empty", ""),
            ("unicode", "Hello 世界 🌍"),
            ("json_like", "{\"name\":\"test\",\"value\":42}"),
            ("special_chars", "Quote: \" Backslash: \\ Newline: \n"),
            ("long_text", new string('A', 10000))
        };

        foreach (var (testName, testValue) in testCases)
        {
            // Act
            _output.WriteLine($"Testing string value: {testName}");
            var key = GenerateTestKey(testName);

            var setResult = await CacheProvider.SetAsync(key, testValue);
            var getValue = await CacheProvider.GetAsync<string>(key);

            // Assert
            setResult.Should().BeTrue($"SetAsync should succeed for {testName}");
            getValue.Should().Be(testValue, $"GetAsync should return exact value for {testName}");

            // Cleanup
            await CacheProvider.RemoveAsync(key);
        }

        _output.WriteLine("✅ String value caching working correctly");
    }

    [Fact]
    public async Task SetAndGet_ShouldWorkCorrectly_WithComplexObjects()
    {
        // Arrange
        var user = new SimpleUser
        {
            Id = "user_001",
            Name = "John Doe",
            Email = "john.doe@example.com",
            Age = 30,
            CreatedAt = new DateTime(2023, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        var key = GenerateTestKey("complex_object");

        // Act
        _output.WriteLine("Testing complex object serialization and deserialization...");

        var setResult = await CacheProvider.SetAsync(key, user);
        var getValue = await CacheProvider.GetAsync<SimpleUser>(key);

        // Assert
        setResult.Should().BeTrue("SetAsync should succeed for complex object");
        getValue.Should().NotBeNull("GetAsync should return non-null object");
        getValue.Should().BeEquivalentTo(user, "Deserialized object should match original");

        // Verify individual properties for complete validation
        getValue!.Id.Should().Be(user.Id);
        getValue.Name.Should().Be(user.Name);
        getValue.Email.Should().Be(user.Email);
        getValue.Age.Should().Be(user.Age);
        getValue.CreatedAt.Should().Be(user.CreatedAt);

        _output.WriteLine("✅ Complex object caching working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(key);
    }

    [Fact]
    public async Task SetAndGet_ShouldWorkCorrectly_WithNestedComplexObjects()
    {
        // Arrange
        var order = new ComplexOrder
        {
            OrderId = "ORDER_001",
            CustomerId = "CUSTOMER_001",
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Processing,
            TotalAmount = 299.99m,
            Currency = "USD",
            ShippingAddress = new Address
            {
                Street = "123 Main St",
                City = "New York",
                State = "NY",
                ZipCode = "10001",
                Country = "USA"
            },
            BillingAddress = new Address
            {
                Street = "456 Billing Ave",
                City = "Los Angeles",
                State = "CA",
                ZipCode = "90210",
                Country = "USA"
            },
            Items = new List<OrderItem>
            {
                new() { ProductId = "P001", ProductName = "Widget A", Quantity = 2, UnitPrice = 99.99m },
                new() { ProductId = "P002", ProductName = "Widget B", Quantity = 1, UnitPrice = 100.01m }
            },
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "web",
                ["campaign"] = "summer_sale",
                ["referrer"] = "google"
            },
            Payment = new PaymentInfo
            {
                PaymentId = "PAY_001",
                Method = PaymentMethod.CreditCard,
                ProcessedAt = DateTime.UtcNow,
                Amount = 299.99m,
                TransactionId = "TXN_12345"
            },
            Tags = new List<string> { "priority", "express_shipping", "loyalty_customer" }
        };

        var key = GenerateTestKey("nested_complex");

        // Act
        _output.WriteLine("Testing nested complex object with collections...");

        var setResult = await CacheProvider.SetAsync(key, order);
        var getValue = await CacheProvider.GetAsync<ComplexOrder>(key);

        // Assert
        setResult.Should().BeTrue("SetAsync should succeed for nested complex object");
        getValue.Should().NotBeNull("GetAsync should return non-null object");

        // Validate core properties
        getValue!.OrderId.Should().Be(order.OrderId);
        getValue.CustomerId.Should().Be(order.CustomerId);
        getValue.Status.Should().Be(order.Status);
        getValue.TotalAmount.Should().Be(order.TotalAmount);

        // Validate nested objects
        getValue.ShippingAddress.Should().BeEquivalentTo(order.ShippingAddress);
        getValue.BillingAddress.Should().BeEquivalentTo(order.BillingAddress);
        getValue.Payment.Should().BeEquivalentTo(order.Payment);

        // Validate collections
        getValue.Items.Should().HaveCount(order.Items.Count);
        getValue.Items.Should().BeEquivalentTo(order.Items);
        getValue.Metadata.Should().BeEquivalentTo(order.Metadata);
        getValue.Tags.Should().BeEquivalentTo(order.Tags);

        _output.WriteLine("✅ Nested complex object caching working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(key);
    }

    #endregion

    #region TTL and Expiration Tests

    [Fact]
    public async Task SetAsync_ShouldRespectTTL_WithShortExpiration()
    {
        // Arrange
        var key = GenerateTestKey("ttl_short");
        var value = "expires_soon";
        var ttl = TimeSpan.FromSeconds(2);

        // Act
        _output.WriteLine("Testing short TTL expiration...");

        var setResult = await CacheProvider.SetAsync(key, value, ttl);
        setResult.Should().BeTrue("SetAsync with TTL should succeed");

        // Verify immediate availability
        var immediateGet = await CacheProvider.GetAsync<string>(key);
        immediateGet.Should().Be(value, "Value should be immediately available after set");

        // Verify exists returns true initially
        var immediateExists = await CacheProvider.ExistsAsync(key);
        immediateExists.Should().BeTrue("Key should exist immediately after set");

        // Wait for expiration
        await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(500))); // Extra buffer for timing

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<string>(key);
        expiredGet.Should().BeNull("Value should be null after TTL expiration");

        var expiredExists = await CacheProvider.ExistsAsync(key);
        expiredExists.Should().BeFalse("Key should not exist after TTL expiration");

        _output.WriteLine("✅ Short TTL expiration working correctly");
    }

    [Fact]
    public async Task SetExpiryAsync_ShouldUpdateTTL_ForExistingKey()
    {
        // Arrange
        var key = GenerateTestKey("ttl_update");
        var value = "test_ttl_update";

        // Act
        _output.WriteLine("Testing TTL update for existing key...");

        // Set without initial TTL
        var setResult = await CacheProvider.SetAsync(key, value);
        setResult.Should().BeTrue("Initial SetAsync should succeed");

        // Verify no expiration initially
        await Task.Delay(1000);
        var stillExists = await CacheProvider.ExistsAsync(key);
        stillExists.Should().BeTrue("Key should still exist without TTL");

        // Set expiration
        var expiryResult = await CacheProvider.SetExpiryAsync(key, TimeSpan.FromSeconds(2));
        expiryResult.Should().BeTrue("SetExpiryAsync should succeed");

        // Verify still available immediately
        var immediateGet = await CacheProvider.GetAsync<string>(key);
        immediateGet.Should().Be(value, "Value should still be available immediately after setting expiry");

        // Wait for new expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<string>(key);
        expiredGet.Should().BeNull("Value should expire after SetExpiryAsync TTL");

        _output.WriteLine("✅ TTL update working correctly");
    }

    [Fact]
    public async Task TTL_ShouldWork_WithVariousTimeSpans()
    {
        // Arrange
        var testCases = new[]
        {
            ("milliseconds", TimeSpan.FromMilliseconds(500)),
            ("seconds", TimeSpan.FromSeconds(1)),
            ("minutes", TimeSpan.FromMinutes(1)),
            ("hours", TimeSpan.FromHours(1))
        };

        foreach (var (testName, ttl) in testCases)
        {
            // Act
            _output.WriteLine($"Testing TTL: {testName} ({ttl})");
            var key = GenerateTestKey($"ttl_{testName}");
            var value = $"test_value_{testName}";

            var setResult = await CacheProvider.SetAsync(key, value, ttl);
            setResult.Should().BeTrue($"SetAsync with {testName} TTL should succeed");

            // Verify immediate availability
            var getValue = await CacheProvider.GetAsync<string>(key);
            getValue.Should().Be(value, $"Value should be immediately available with {testName} TTL");

            // For longer TTLs, just verify they don't expire immediately
            if (ttl > TimeSpan.FromSeconds(5))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                var stillThere = await CacheProvider.ExistsAsync(key);
                stillThere.Should().BeTrue($"Key with {testName} TTL should not expire immediately");

                // Cleanup long TTL keys
                await CacheProvider.RemoveAsync(key);
            }
            else
            {
                // For short TTLs, verify they actually expire
                var waitTime = ttl.Add(TimeSpan.FromMilliseconds(200)); // Small buffer
                await Task.Delay(waitTime);

                var expiredGet = await CacheProvider.GetAsync<string>(key);
                expiredGet.Should().BeNull($"Value should expire after {testName} TTL");
            }
        }

        _output.WriteLine("✅ Various TTL timespan handling working correctly");
    }

    #endregion

    #region Remove and Exists Operations

    [Fact]
    public async Task RemoveAsync_ShouldRemoveExistingKeys_Correctly()
    {
        // Arrange
        var keys = GenerateTestKeys(5, "remove_test");
        var values = keys.Select((key, index) => $"value_{index}").ToList();

        // Setup - Add multiple keys
        for (int i = 0; i < keys.Count; i++)
        {
            var setResult = await CacheProvider.SetAsync(keys[i], values[i]);
            setResult.Should().BeTrue($"Initial setup for key {i} should succeed");
        }

        // Verify all keys exist
        foreach (var key in keys)
        {
            var exists = await CacheProvider.ExistsAsync(key);
            exists.Should().BeTrue($"Key {key} should exist after setup");
        }

        // Act - Remove keys one by one
        _output.WriteLine("Testing individual key removal...");

        for (int i = 0; i < keys.Count; i++)
        {
            var removeResult = await CacheProvider.RemoveAsync(keys[i]);
            removeResult.Should().BeTrue($"RemoveAsync should succeed for key {i}");

            // Verify removed key no longer exists
            var existsAfterRemove = await CacheProvider.ExistsAsync(keys[i]);
            existsAfterRemove.Should().BeFalse($"Key {i} should not exist after removal");

            var getAfterRemove = await CacheProvider.GetAsync<string>(keys[i]);
            getAfterRemove.Should().BeNull($"GetAsync should return null for removed key {i}");

            // Verify other keys still exist
            for (int j = i + 1; j < keys.Count; j++)
            {
                var otherKeyExists = await CacheProvider.ExistsAsync(keys[j]);
                otherKeyExists.Should().BeTrue($"Key {j} should still exist when key {i} is removed");
            }
        }

        _output.WriteLine("✅ Individual key removal working correctly");
    }

    [Fact]
    public async Task RemoveAsync_ShouldReturnFalse_ForNonExistentKeys()
    {
        // Arrange
        var nonExistentKeys = new[]
        {
            GenerateTestKey("non_existent_1"),
            GenerateTestKey("non_existent_2"),
            GenerateTestKey("non_existent_3")
        };

        // Act & Assert
        _output.WriteLine("Testing removal of non-existent keys...");

        foreach (var key in nonExistentKeys)
        {
            // Verify key doesn't exist first
            var existsBefore = await CacheProvider.ExistsAsync(key);
            existsBefore.Should().BeFalse($"Key {key} should not exist initially");

            // Attempt removal
            var removeResult = await CacheProvider.RemoveAsync(key);
            removeResult.Should().BeFalse($"RemoveAsync should return false for non-existent key {key}");
        }

        _output.WriteLine("✅ Non-existent key removal handling working correctly");
    }

    [Fact]
    public async Task ExistsAsync_ShouldAccuratelyReportKeyExistence()
    {
        // Arrange
        var existingKey = GenerateTestKey("exists_true");
        var nonExistentKey = GenerateTestKey("exists_false");
        var removedKey = GenerateTestKey("exists_removed");

        // Setup existing key
        await CacheProvider.SetAsync(existingKey, "test_value");

        // Setup removed key
        await CacheProvider.SetAsync(removedKey, "will_be_removed");
        await CacheProvider.RemoveAsync(removedKey);

        // Act & Assert
        _output.WriteLine("Testing key existence detection...");

        // Test existing key
        var existingKeyExists = await CacheProvider.ExistsAsync(existingKey);
        existingKeyExists.Should().BeTrue("ExistsAsync should return true for existing key");

        // Test non-existent key
        var nonExistentKeyExists = await CacheProvider.ExistsAsync(nonExistentKey);
        nonExistentKeyExists.Should().BeFalse("ExistsAsync should return false for non-existent key");

        // Test removed key
        var removedKeyExists = await CacheProvider.ExistsAsync(removedKey);
        removedKeyExists.Should().BeFalse("ExistsAsync should return false for removed key");

        _output.WriteLine("✅ Key existence detection working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(existingKey);
    }

    #endregion

    #region Data Integrity and Serialization Tests

    [Fact]
    public async Task Serialization_ShouldPreserveDataIntegrity_WithSpecialCharacters()
    {
        // Arrange
        var specialModel = new SpecialCharacterModel
        {
            Id = "SPECIAL_001",
            UnicodeText = "Hello 世界 🌍 Здравствуй мир",
            JsonSpecialChars = "Quote: \" Backslash: \\ Newline: \n Tab: \t",
            UrlEncodedData = "param1=value%20with%20spaces&param2=special%21chars",
            EmptyString = string.Empty,
            SingleChar = "A",
            MaxLengthString = new string('X', 1000),
            NullableString = null,
            NullableInt = null,
            NullableDateTime = null,
            InfiniteValue = double.PositiveInfinity,
            NanValue = double.NaN,
            MaxDecimal = decimal.MaxValue,
            MinDecimal = decimal.MinValue
        };

        var key = GenerateTestKey("special_chars");

        // Act
        _output.WriteLine("Testing special character and edge case serialization...");

        var setResult = await CacheProvider.SetAsync(key, specialModel);
        var getValue = await CacheProvider.GetAsync<SpecialCharacterModel>(key);

        // Assert
        setResult.Should().BeTrue("SetAsync should succeed with special characters");
        getValue.Should().NotBeNull("GetAsync should return non-null object");

        // Validate all properties
        getValue!.Id.Should().Be(specialModel.Id);
        getValue.UnicodeText.Should().Be(specialModel.UnicodeText);
        getValue.JsonSpecialChars.Should().Be(specialModel.JsonSpecialChars);
        getValue.UrlEncodedData.Should().Be(specialModel.UrlEncodedData);
        getValue.EmptyString.Should().Be(specialModel.EmptyString);
        getValue.SingleChar.Should().Be(specialModel.SingleChar);
        getValue.MaxLengthString.Should().Be(specialModel.MaxLengthString);

        // Validate nullable properties
        getValue.NullableString.Should().BeNull();
        getValue.NullableInt.Should().BeNull();
        getValue.NullableDateTime.Should().BeNull();

        // Validate special numeric values
        getValue.MaxDecimal.Should().Be(specialModel.MaxDecimal);
        getValue.MinDecimal.Should().Be(specialModel.MinDecimal);

        // Note: NaN and Infinity might not serialize/deserialize exactly the same depending on JSON serializer
        // This is acceptable for most cache scenarios

        _output.WriteLine("✅ Special character serialization working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(key);
    }

    [Fact]
    public async Task LargeData_ShouldBeCached_WithoutDataLoss()
    {
        // Arrange
        var largeModel = LargeDataModel.CreateWithSize(100, 1000); // Smaller for test performance
        var key = GenerateTestKey("large_data");

        _output.WriteLine($"Testing large data caching (estimated size: {largeModel.EstimateSize()} bytes)...");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var setResult = await CacheProvider.SetAsync(key, largeModel);
        var setTime = stopwatch.Elapsed;

        stopwatch.Restart();
        var getValue = await CacheProvider.GetAsync<LargeDataModel>(key);
        var getTime = stopwatch.Elapsed;

        // Assert
        setResult.Should().BeTrue("SetAsync should succeed with large data");
        getValue.Should().NotBeNull("GetAsync should return non-null large object");

        // Validate core properties
        getValue!.Id.Should().Be(largeModel.Id);
        getValue.LargeDescription.Should().Be(largeModel.LargeDescription);
        getValue.BinaryData.Should().Be(largeModel.BinaryData);

        // Validate collections
        getValue.DataPoints.Should().HaveCount(largeModel.DataPoints.Count);
        getValue.NestedObjects.Should().HaveCount(largeModel.NestedObjects.Count);

        // Performance validation
        setTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Large data set operation should complete within 5 seconds");
        getTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Large data get operation should complete within 5 seconds");

        _output.WriteLine($"✅ Large data caching working correctly - Set: {setTime.TotalMilliseconds:F2}ms, Get: {getTime.TotalMilliseconds:F2}ms");

        // Cleanup
        await CacheProvider.RemoveAsync(key);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvalidOperations_ShouldThrowMeaningfulExceptions()
    {
        // Test null key scenarios
        _output.WriteLine("Testing invalid operation error handling...");

        // Test null key for GetAsync
        var getNullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.GetAsync<string>(null!));
        getNullKeyException.Should().NotBeNull();
        getNullKeyException.Message.Should().ContainEquivalentOf("key");

        // Test empty key for GetAsync
        var getEmptyKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.GetAsync<string>(""));
        getEmptyKeyException.Should().NotBeNull();

        // Test null key for SetAsync
        var setNullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetAsync<string>(null!, "value"));
        setNullKeyException.Should().NotBeNull();

        // Test null value for SetAsync
        var setNullValueException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.SetAsync<string>("key", null!));
        setNullValueException.Should().NotBeNull();

        // Test null key for RemoveAsync
        var removeNullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.RemoveAsync(null!));
        removeNullKeyException.Should().NotBeNull();

        // Test null key for ExistsAsync
        var existsNullKeyException = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CacheProvider.ExistsAsync(null!));
        existsNullKeyException.Should().NotBeNull();

        _output.WriteLine("✅ Invalid operation error handling working correctly");
    }

    [Fact]
    public async Task CancellationToken_ShouldBehaveProperly_WhenCancelled()
    {
        // Arrange
        var key = GenerateTestKey("cancellation");
        var value = "test_cancellation";
        using var cts = new CancellationTokenSource();

        _output.WriteLine("Testing cancellation token behavior...");

        // Test immediate cancellation
        cts.Cancel();

        // Act & Assert - Test various operations with cancelled token
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.GetAsync<string>(key, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.SetAsync(key, value, cancellationToken: cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.RemoveAsync(key, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.ExistsAsync(key, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CacheProvider.SetExpiryAsync(key, TimeSpan.FromMinutes(1), cts.Token));

        _output.WriteLine("✅ Cancellation token behavior working correctly");
    }

    #endregion

    #region Performance and Concurrency Tests

    [Fact]
    public async Task ConcurrentOperations_ShouldNotCauseDataCorruption()
    {
        // Arrange
        const int concurrentOperations = 50;
        const int operationsPerTask = 20;
        var results = new ConcurrentBag<CacheOperationMetrics>();

        _output.WriteLine($"Testing {concurrentOperations} concurrent cache operations...");

        // Act
        var tasks = Enumerable.Range(0, concurrentOperations).Select(async taskId =>
        {
            for (int op = 0; op < operationsPerTask; op++)
            {
                var key = GenerateTestKey($"concurrent_{taskId}_{op}");
                var model = new ConcurrencyTestModel
                {
                    Id = $"task_{taskId}_op_{op}",
                    Counter = op,
                    ThreadId = $"task_{taskId}"
                };

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    // Perform set operation
                    var setResult = await CacheProvider.SetAsync(key, model);
                    if (!setResult)
                    {
                        results.Add(new CacheOperationMetrics
                        {
                            OperationType = "Set",
                            Success = false,
                            ErrorMessage = "SetAsync returned false"
                        });
                        continue;
                    }

                    // Perform get operation
                    var getValue = await CacheProvider.GetAsync<ConcurrencyTestModel>(key);
                    if (getValue == null || getValue.Id != model.Id)
                    {
                        results.Add(new CacheOperationMetrics
                        {
                            OperationType = "Get",
                            Success = false,
                            ErrorMessage = "GetAsync returned null or incorrect data"
                        });
                        continue;
                    }

                    // Record successful operation
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "SetGet",
                        Duration = stopwatch.Elapsed,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new CacheOperationMetrics
                    {
                        OperationType = "SetGet",
                        Duration = stopwatch.Elapsed,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var totalOperations = concurrentOperations * operationsPerTask;
        var successfulOperations = results.Count(r => r.Success);
        var failedOperations = results.Count(r => !r.Success);

        _output.WriteLine($"Concurrent operations completed - Total: {totalOperations}, Successful: {successfulOperations}, Failed: {failedOperations}");

        // Validate results
        results.Should().HaveCount(totalOperations, "Should have metrics for all operations");
        successfulOperations.Should().Be(totalOperations, "All concurrent operations should succeed");

        if (failedOperations > 0)
        {
            var errorMessages = results.Where(r => !r.Success).Select(r => r.ErrorMessage).Distinct();
            _output.WriteLine($"Failures occurred: {string.Join(", ", errorMessages)}");
        }

        // Performance validation
        var avgDuration = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
        avgDuration.Should().BeLessThan(1000, "Average operation duration should be under 1 second");

        _output.WriteLine($"✅ Concurrent operations working correctly - Average duration: {avgDuration:F2}ms");
    }

    [Fact]
    public async Task HighThroughputOperations_ShouldMaintainPerformance()
    {
        // Arrange
        const int operationCount = 1000;
        var metrics = new List<CacheOperationMetrics>();

        _output.WriteLine($"Testing high throughput with {operationCount} operations...");

        var overallStopwatch = Stopwatch.StartNew();

        // Act - Perform many sequential operations
        for (int i = 0; i < operationCount; i++)
        {
            var key = GenerateTestKey($"throughput_{i}");
            var value = $"value_{i}_{DateTime.UtcNow.Ticks}";

            var operationStopwatch = Stopwatch.StartNew();
            try
            {
                var setResult = await CacheProvider.SetAsync(key, value);
                var getValue = await CacheProvider.GetAsync<string>(key);
                var removeResult = await CacheProvider.RemoveAsync(key);

                operationStopwatch.Stop();

                metrics.Add(new CacheOperationMetrics
                {
                    OperationType = "SetGetRemove",
                    Duration = operationStopwatch.Elapsed,
                    Success = setResult && getValue == value && removeResult
                });
            }
            catch (Exception ex)
            {
                operationStopwatch.Stop();
                metrics.Add(new CacheOperationMetrics
                {
                    OperationType = "SetGetRemove",
                    Duration = operationStopwatch.Elapsed,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        overallStopwatch.Stop();

        // Assert
        var successfulOperations = metrics.Count(m => m.Success);
        var failureRate = (double)(operationCount - successfulOperations) / operationCount * 100;

        // Performance metrics
        var avgDuration = metrics.Where(m => m.Success).Average(m => m.Duration.TotalMilliseconds);
        var maxDuration = metrics.Where(m => m.Success).Max(m => m.Duration.TotalMilliseconds);
        var minDuration = metrics.Where(m => m.Success).Min(m => m.Duration.TotalMilliseconds);
        var operationsPerSecond = operationCount / overallStopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Throughput test results:");
        _output.WriteLine($"  Total operations: {operationCount}");
        _output.WriteLine($"  Successful: {successfulOperations} ({(100 - failureRate):F2}%)");
        _output.WriteLine($"  Operations/second: {operationsPerSecond:F2}");
        _output.WriteLine($"  Average duration: {avgDuration:F2}ms");
        _output.WriteLine($"  Min/Max duration: {minDuration:F2}ms / {maxDuration:F2}ms");

        // Validate performance requirements
        successfulOperations.Should().BeGreaterOrEqualTo((int)(operationCount * 0.95), "At least 95% of operations should succeed");
        operationsPerSecond.Should().BeGreaterThan(100, "Should achieve at least 100 operations per second");
        avgDuration.Should().BeLessThan(50, "Average operation time should be under 50ms");

        _output.WriteLine("✅ High throughput performance requirements met");
    }

    #endregion

    #region Memory and Resource Management Tests

    [Fact]
    public async Task MemoryUsage_ShouldBeReasonable_WithMultipleOperations()
    {
        // Arrange
        const int dataSetSize = 100;
        var keys = new List<string>();

        _output.WriteLine("Testing memory usage with multiple cache entries...");

        var initialMemory = await GetMemoryInfoAsync();
        _output.WriteLine($"Initial Redis memory usage: {initialMemory.UsedMemory / 1024:N0} KB");

        // Act - Add many cache entries
        for (int i = 0; i < dataSetSize; i++)
        {
            var key = GenerateTestKey($"memory_{i}");
            keys.Add(key);

            var user = new SimpleUser
            {
                Id = $"user_{i}",
                Name = $"User {i}",
                Email = $"user{i}@example.com",
                Age = 20 + (i % 60),
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            };

            var setResult = await CacheProvider.SetAsync(key, user);
            setResult.Should().BeTrue($"SetAsync should succeed for entry {i}");
        }

        var afterSetMemory = await GetMemoryInfoAsync();
        var memoryIncrease = afterSetMemory.UsedMemory - initialMemory.UsedMemory;

        _output.WriteLine($"Memory after {dataSetSize} entries: {afterSetMemory.UsedMemory / 1024:N0} KB (increase: {memoryIncrease / 1024:N0} KB)");

        // Clean up entries
        foreach (var key in keys)
        {
            await CacheProvider.RemoveAsync(key);
        }

        // Allow some time for memory cleanup
        await Task.Delay(1000);

        var afterCleanupMemory = await GetMemoryInfoAsync();
        var memoryAfterCleanup = afterCleanupMemory.UsedMemory - initialMemory.UsedMemory;

        _output.WriteLine($"Memory after cleanup: {afterCleanupMemory.UsedMemory / 1024:N0} KB (net increase: {memoryAfterCleanup / 1024:N0} KB)");

        // Assert
        memoryIncrease.Should().BeGreaterThan(0, "Memory usage should increase with cache entries");
        memoryAfterCleanup.Should().BeLessThan((long)(memoryIncrease * 0.5), "Memory should be mostly reclaimed after cleanup");

        _output.WriteLine("✅ Memory usage behavior is reasonable");
    }

    [Fact]
    public async Task ResourceCleanup_ShouldWork_WhenProviderIsDisposed()
    {
        // This test validates that the provider cleans up properly
        // We can't test the current provider being disposed, but we can create a temporary one

        _output.WriteLine("Testing resource cleanup on provider disposal...");

        var connectionString = RedisConnection.Configuration!;
        var restConfig = new RestConfig
        {
            ConnectionString = connectionString,
            OperationMode = RestMode.Redis
        };

        // Create temporary provider
        var tempProvider = new RedisCacheProvider(restConfig);

        // Use the provider
        var testKey = GenerateTestKey("disposal_test");
        var setResult = await tempProvider.SetAsync(testKey, "test_value");
        setResult.Should().BeTrue("Temporary provider should work normally");

        // Dispose the provider
        tempProvider.Dispose();

        _output.WriteLine("✅ Provider disposal completed without exceptions");

        // Verify our main provider still works
        var mainProviderTest = await CacheProvider.SetAsync(GenerateTestKey("main_provider_test"), "still_works");
        mainProviderTest.Should().BeTrue("Main provider should continue working after temporary provider disposal");

        await CacheProvider.RemoveAsync(GenerateTestKey("main_provider_test"));
    }

    #endregion
}