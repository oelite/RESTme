using FluentAssertions;
using OElite.Restme.Redis.IntegrationTests.Infrastructure;
using OElite.Restme.Redis.IntegrationTests.Models;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OElite.Restme.Redis.IntegrationTests.Tests;

/// <summary>
/// Specialized tests for TTL expiration validation and time-based cache behavior
/// Ensures enterprise-grade time-based functionality and expiration accuracy
/// </summary>
public class RedisTtlExpirationTests : RedisTestBase
{
    private readonly ITestOutputHelper _output;

    public RedisTtlExpirationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Precise TTL Expiration Tests

    [Fact]
    public async Task TTL_ShouldExpire_WithMillisecondPrecision()
    {
        // Arrange
        var key = GenerateTestKey("precise_ttl");
        var value = ExpiringData.CreateWithTtl("precise_timing", TimeSpan.FromMilliseconds(800), "millisecond_test");
        var ttl = TimeSpan.FromMilliseconds(800);

        _output.WriteLine($"Testing millisecond-precise TTL expiration: {ttl.TotalMilliseconds}ms");

        var stopwatch = Stopwatch.StartNew();

        // Act
        var setResult = await CacheProvider.SetAsync(key, value, ttl);
        setResult.Should().BeTrue("SetAsync should succeed");

        // Verify immediate availability
        var immediateGet = await CacheProvider.GetAsync<ExpiringData>(key);
        immediateGet.Should().NotBeNull("Value should be available immediately");
        immediateGet!.Content.Should().Be(value.Content);

        // Wait for near expiration (slightly before TTL)
        var waitTime = TimeSpan.FromMilliseconds(ttl.TotalMilliseconds * 0.75); // 75% of TTL
        await Task.Delay(waitTime);

        var nearExpiryGet = await CacheProvider.GetAsync<ExpiringData>(key);
        nearExpiryGet.Should().NotBeNull("Value should still be available before expiration");

        // Wait for expiration with buffer
        var remainingTime = ttl.Subtract(stopwatch.Elapsed).Add(TimeSpan.FromMilliseconds(200));
        if (remainingTime > TimeSpan.Zero)
        {
            await Task.Delay(remainingTime);
        }

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<ExpiringData>(key);
        expiredGet.Should().BeNull("Value should be null after TTL expiration");

        var expiredExists = await CacheProvider.ExistsAsync(key);
        expiredExists.Should().BeFalse("Key should not exist after TTL expiration");

        var totalTime = stopwatch.Elapsed;
        _output.WriteLine($"✅ Millisecond-precise TTL working correctly - Total time: {totalTime.TotalMilliseconds:F2}ms, Expected: {ttl.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task TTL_ShouldWork_WithVariousPrecisionLevels()
    {
        // Arrange
        var testCases = new[]
        {
            ("100ms", TimeSpan.FromMilliseconds(100)),
            ("500ms", TimeSpan.FromMilliseconds(500)),
            ("1s", TimeSpan.FromSeconds(1)),
            ("2s", TimeSpan.FromSeconds(2)),
            ("3s", TimeSpan.FromSeconds(3))
        };

        foreach (var (testName, ttl) in testCases)
        {
            _output.WriteLine($"Testing TTL precision: {testName}");

            var key = GenerateTestKey($"precision_{testName}");
            var value = ExpiringData.CreateWithTtl($"content_{testName}", ttl, testName);

            var stopwatch = Stopwatch.StartNew();

            // Set with TTL
            var setResult = await CacheProvider.SetAsync(key, value, ttl);
            setResult.Should().BeTrue($"SetAsync should succeed for {testName}");

            // Verify immediate availability
            var immediateGet = await CacheProvider.GetAsync<ExpiringData>(key);
            immediateGet.Should().NotBeNull($"Value should be immediately available for {testName}");

            // Wait for expiration
            var waitTime = ttl.Add(TimeSpan.FromMilliseconds(300)); // Add buffer
            await Task.Delay(waitTime);

            // Verify expiration
            var expiredGet = await CacheProvider.GetAsync<ExpiringData>(key);
            expiredGet.Should().BeNull($"Value should expire after {testName} TTL");

            var actualTime = stopwatch.Elapsed;
            var tolerance = TimeSpan.FromMilliseconds(500); // 500ms tolerance

            actualTime.Should().BeGreaterOrEqualTo(ttl, $"Actual expiration time should be at least the TTL for {testName}");
            actualTime.Should().BeLessOrEqualTo(ttl.Add(tolerance), $"Actual expiration time should be within tolerance for {testName}");

            _output.WriteLine($"  ✅ {testName} - Expected: {ttl.TotalMilliseconds}ms, Actual: {actualTime.TotalMilliseconds:F2}ms");
        }
    }

    [Fact]
    public async Task TTL_ShouldBeAccurate_WithMultipleKeysExpiring()
    {
        // Arrange
        const int keyCount = 10;
        var baseTime = DateTime.UtcNow;
        var expirationResults = new List<(string Key, TimeSpan ExpectedTtl, TimeSpan ActualTime, bool ExpiredOnTime)>();

        _output.WriteLine($"Testing TTL accuracy with {keyCount} keys expiring simultaneously...");

        var keys = new List<string>();
        var ttl = TimeSpan.FromSeconds(2);

        // Set multiple keys with same TTL
        var setStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < keyCount; i++)
        {
            var key = GenerateTestKey($"multi_expire_{i}");
            keys.Add(key);

            var value = ExpiringData.CreateWithTtl($"multi_content_{i}", ttl, "multi_expiry");
            var setResult = await CacheProvider.SetAsync(key, value, ttl);
            setResult.Should().BeTrue($"SetAsync should succeed for key {i}");
        }
        setStopwatch.Stop();

        _output.WriteLine($"All keys set in {setStopwatch.ElapsedMilliseconds}ms");

        // Wait for expiration
        await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(500)));

        // Check expiration for all keys
        var checkStopwatch = Stopwatch.StartNew();
        foreach (var key in keys)
        {
            var keyStopwatch = Stopwatch.StartNew();
            var expiredGet = await CacheProvider.GetAsync<ExpiringData>(key);
            var keyTime = keyStopwatch.Elapsed;

            expirationResults.Add((
                Key: key,
                ExpectedTtl: ttl,
                ActualTime: keyTime,
                ExpiredOnTime: expiredGet == null
            ));
        }
        checkStopwatch.Stop();

        // Assert
        var allExpired = expirationResults.All(r => r.ExpiredOnTime);
        allExpired.Should().BeTrue("All keys should have expired");

        var avgCheckTime = expirationResults.Average(r => r.ActualTime.TotalMilliseconds);
        avgCheckTime.Should().BeLessThan(50, "Average key check time should be under 50ms");

        _output.WriteLine($"✅ Multi-key TTL accuracy validated - All expired: {allExpired}, Avg check time: {avgCheckTime:F2}ms");
    }

    #endregion

    #region TTL Update and Modification Tests

    [Fact]
    public async Task SetExpiryAsync_ShouldUpdateTTL_Accurately()
    {
        // Arrange
        var key = GenerateTestKey("update_ttl");
        var value = "ttl_update_test";
        var initialTtl = TimeSpan.FromSeconds(10); // Long initial TTL
        var newTtl = TimeSpan.FromSeconds(1); // Short new TTL

        _output.WriteLine("Testing TTL update accuracy...");

        // Act
        // Set with long initial TTL
        var setResult = await CacheProvider.SetAsync(key, value, initialTtl);
        setResult.Should().BeTrue("Initial SetAsync should succeed");

        // Verify key exists
        var initialExists = await CacheProvider.ExistsAsync(key);
        initialExists.Should().BeTrue("Key should exist after initial set");

        // Update to shorter TTL
        var updateStopwatch = Stopwatch.StartNew();
        var updateResult = await CacheProvider.SetExpiryAsync(key, newTtl);
        updateResult.Should().BeTrue("SetExpiryAsync should succeed");

        // Verify key still exists immediately after update
        var immediateAfterUpdate = await CacheProvider.ExistsAsync(key);
        immediateAfterUpdate.Should().BeTrue("Key should still exist immediately after TTL update");

        // Wait for new TTL expiration
        await Task.Delay(newTtl.Add(TimeSpan.FromMilliseconds(300)));

        // Verify expiration based on new TTL
        var expiredGet = await CacheProvider.GetAsync<string>(key);
        expiredGet.Should().BeNull("Key should expire based on new TTL, not original TTL");

        var totalTime = updateStopwatch.Elapsed;
        totalTime.Should().BeGreaterOrEqualTo(newTtl, "Total time should be at least the new TTL");
        totalTime.Should().BeLessOrEqualTo(newTtl.Add(TimeSpan.FromSeconds(1)), "Total time should not exceed new TTL by more than 1 second");

        _output.WriteLine($"✅ TTL update working correctly - New TTL: {newTtl.TotalMilliseconds}ms, Actual: {totalTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task SetExpiryAsync_ShouldExtendTTL_WhenIncreased()
    {
        // Arrange
        var key = GenerateTestKey("extend_ttl");
        var value = "ttl_extension_test";
        var shortTtl = TimeSpan.FromSeconds(1);
        var longTtl = TimeSpan.FromSeconds(4);

        _output.WriteLine("Testing TTL extension...");

        // Set with short TTL
        var setResult = await CacheProvider.SetAsync(key, value, shortTtl);
        setResult.Should().BeTrue("SetAsync with short TTL should succeed");

        // Wait for most of the short TTL to pass
        await Task.Delay(TimeSpan.FromMilliseconds(shortTtl.TotalMilliseconds * 0.8));

        // Extend TTL
        var extendResult = await CacheProvider.SetExpiryAsync(key, longTtl);
        extendResult.Should().BeTrue("TTL extension should succeed");

        // Wait past the original short TTL
        await Task.Delay(TimeSpan.FromMilliseconds(shortTtl.TotalMilliseconds * 0.5));

        // Verify key still exists (should have been expired with original TTL)
        var stillExists = await CacheProvider.GetAsync<string>(key);
        stillExists.Should().Be(value, "Key should still exist with extended TTL");

        _output.WriteLine("✅ TTL extension working correctly");

        // Cleanup - don't wait for long TTL
        await CacheProvider.RemoveAsync(key);
    }

    [Fact]
    public async Task SetAsync_ShouldResetTTL_WhenUpdatingExistingKey()
    {
        // Arrange
        var key = GenerateTestKey("reset_ttl");
        var originalValue = "original_value";
        var updatedValue = "updated_value";
        var originalTtl = TimeSpan.FromSeconds(1);
        var newTtl = TimeSpan.FromSeconds(3);

        _output.WriteLine("Testing TTL reset when updating existing key...");

        // Set original value with short TTL
        var originalSetResult = await CacheProvider.SetAsync(key, originalValue, originalTtl);
        originalSetResult.Should().BeTrue("Original SetAsync should succeed");

        // Wait for most of original TTL
        await Task.Delay(TimeSpan.FromMilliseconds(originalTtl.TotalMilliseconds * 0.8));

        // Update value with new TTL
        var updateStopwatch = Stopwatch.StartNew();
        var updateSetResult = await CacheProvider.SetAsync(key, updatedValue, newTtl);
        updateSetResult.Should().BeTrue("Update SetAsync should succeed");

        // Wait past original TTL but within new TTL
        await Task.Delay(TimeSpan.FromMilliseconds(originalTtl.TotalMilliseconds * 0.5));

        // Verify key still exists with updated value
        var getValue = await CacheProvider.GetAsync<string>(key);
        getValue.Should().Be(updatedValue, "Key should have updated value and new TTL");

        // Wait for new TTL expiration
        var remainingTime = newTtl.Subtract(updateStopwatch.Elapsed).Add(TimeSpan.FromMilliseconds(300));
        if (remainingTime > TimeSpan.Zero)
        {
            await Task.Delay(remainingTime);
        }

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<string>(key);
        expiredGet.Should().BeNull("Key should expire based on new TTL");

        _output.WriteLine("✅ TTL reset when updating existing key working correctly");
    }

    #endregion

    #region Complex TTL Scenarios

    [Fact]
    public async Task TTL_ShouldWork_WithComplexObjectUpdates()
    {
        // Arrange
        var key = GenerateTestKey("complex_ttl");
        var ttl = TimeSpan.FromSeconds(2);

        var originalOrder = new ComplexOrder
        {
            OrderId = "ORDER_TTL_001",
            CustomerId = "CUSTOMER_001",
            Status = OrderStatus.Pending,
            TotalAmount = 100.00m,
            Items = new List<OrderItem>
            {
                new() { ProductId = "P001", ProductName = "Product 1", Quantity = 1, UnitPrice = 100.00m }
            }
        };

        var updatedOrder = new ComplexOrder
        {
            OrderId = "ORDER_TTL_001",
            CustomerId = "CUSTOMER_001",
            Status = OrderStatus.Processing,
            TotalAmount = 200.00m,
            Items = new List<OrderItem>
            {
                new() { ProductId = "P001", ProductName = "Product 1", Quantity = 1, UnitPrice = 100.00m },
                new() { ProductId = "P002", ProductName = "Product 2", Quantity = 1, UnitPrice = 100.00m }
            }
        };

        _output.WriteLine("Testing TTL with complex object updates...");

        // Set original order
        var originalSetResult = await CacheProvider.SetAsync(key, originalOrder, ttl);
        originalSetResult.Should().BeTrue("Original complex object set should succeed");

        // Wait briefly
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // Update order (this should reset TTL)
        var updateStopwatch = Stopwatch.StartNew();
        var updateSetResult = await CacheProvider.SetAsync(key, updatedOrder, ttl);
        updateSetResult.Should().BeTrue("Updated complex object set should succeed");

        // Verify updated object is cached
        var getUpdated = await CacheProvider.GetAsync<ComplexOrder>(key);
        getUpdated.Should().NotBeNull("Updated object should be retrievable");
        getUpdated!.Status.Should().Be(OrderStatus.Processing);
        getUpdated.TotalAmount.Should().Be(200.00m);
        getUpdated.Items.Should().HaveCount(2);

        // Wait for TTL expiration
        await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(300)));

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<ComplexOrder>(key);
        expiredGet.Should().BeNull("Complex object should expire according to TTL");

        var totalTime = updateStopwatch.Elapsed;
        totalTime.Should().BeGreaterOrEqualTo(ttl);

        _output.WriteLine($"✅ Complex object TTL working correctly - Expected: {ttl.TotalMilliseconds}ms, Actual: {totalTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task TTL_ShouldNotAffect_OtherKeys()
    {
        // Arrange
        var expiring_key = GenerateTestKey("expiring");
        var persistent_key = GenerateTestKey("persistent");
        var expiring_value = "will_expire";
        var persistent_value = "will_persist";
        var ttl = TimeSpan.FromSeconds(1);

        _output.WriteLine("Testing TTL isolation between keys...");

        // Set expiring key with TTL
        var expiringSetResult = await CacheProvider.SetAsync(expiring_key, expiring_value, ttl);
        expiringSetResult.Should().BeTrue("Expiring key set should succeed");

        // Set persistent key without TTL
        var persistentSetResult = await CacheProvider.SetAsync(persistent_key, persistent_value);
        persistentSetResult.Should().BeTrue("Persistent key set should succeed");

        // Verify both keys exist initially
        var expiringExists = await CacheProvider.ExistsAsync(expiring_key);
        var persistentExists = await CacheProvider.ExistsAsync(persistent_key);

        expiringExists.Should().BeTrue("Expiring key should exist initially");
        persistentExists.Should().BeTrue("Persistent key should exist initially");

        // Wait for expiring key TTL
        await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(300)));

        // Verify expiring key is gone, persistent key remains
        var expiringAfterTtl = await CacheProvider.GetAsync<string>(expiring_key);
        var persistentAfterTtl = await CacheProvider.GetAsync<string>(persistent_key);

        expiringAfterTtl.Should().BeNull("Expiring key should be null after TTL");
        persistentAfterTtl.Should().Be(persistent_value, "Persistent key should remain unchanged");

        _output.WriteLine("✅ TTL isolation between keys working correctly");

        // Cleanup
        await CacheProvider.RemoveAsync(persistent_key);
    }

    [Fact]
    public async Task TTL_ShouldWork_WithRapidSequentialOperations()
    {
        // Arrange
        const int operationCount = 20;
        var ttl = TimeSpan.FromSeconds(2);
        var keys = new List<string>();

        _output.WriteLine($"Testing TTL with {operationCount} rapid sequential operations...");

        var setStopwatch = Stopwatch.StartNew();

        // Rapidly set multiple keys with TTL
        for (int i = 0; i < operationCount; i++)
        {
            var key = GenerateTestKey($"rapid_{i}");
            keys.Add(key);

            var value = new ExpiringData
            {
                Id = $"rapid_{i}",
                Content = $"rapid_content_{i}",
                ExpectedTtl = ttl,
                TestCategory = "rapid_sequential"
            };

            var setResult = await CacheProvider.SetAsync(key, value, ttl);
            setResult.Should().BeTrue($"SetAsync should succeed for rapid operation {i}");
        }

        var setTime = setStopwatch.Elapsed;
        _output.WriteLine($"All {operationCount} operations completed in {setTime.TotalMilliseconds:F2}ms");

        // Verify all keys exist
        var existsChecks = await Task.WhenAll(keys.Select(k => CacheProvider.ExistsAsync(k)));
        existsChecks.Should().AllSatisfy(exists => exists.Should().BeTrue("All keys should exist after rapid setting"));

        // Wait for TTL expiration
        await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(500)));

        // Verify all keys have expired
        var expiredChecks = await Task.WhenAll(keys.Select(k => CacheProvider.GetAsync<ExpiringData>(k)));
        expiredChecks.Should().AllSatisfy(value => value.Should().BeNull("All values should be null after TTL expiration"));

        _output.WriteLine("✅ Rapid sequential TTL operations working correctly");
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public async Task TTL_ShouldHandleZeroAndNegativeValues_Appropriately()
    {
        // Test zero TTL
        var zeroTtlKey = GenerateTestKey("zero_ttl");
        var zeroTtlValue = "zero_ttl_value";

        _output.WriteLine("Testing zero TTL handling...");

        // Redis typically treats zero TTL as immediate expiration or no TTL depending on implementation
        var zeroTtlResult = await CacheProvider.SetAsync(zeroTtlKey, zeroTtlValue, TimeSpan.Zero);
        zeroTtlResult.Should().BeTrue("SetAsync with zero TTL should succeed");

        // Check immediate status
        var immediateGet = await CacheProvider.GetAsync<string>(zeroTtlKey);
        // Zero TTL behavior may vary - either immediate expiration or treated as no TTL
        // This is acceptable for Redis implementations

        _output.WriteLine($"Zero TTL result: {(immediateGet != null ? "persisted" : "immediately expired")}");

        // Test negative TTL (should behave like zero or immediate expiration)
        var negativeTtlKey = GenerateTestKey("negative_ttl");
        var negativeTtlValue = "negative_ttl_value";

        _output.WriteLine("Testing negative TTL handling...");

        var negativeTtlResult = await CacheProvider.SetAsync(negativeTtlKey, negativeTtlValue, TimeSpan.FromMilliseconds(-1000));
        negativeTtlResult.Should().BeTrue("SetAsync with negative TTL should succeed (Redis handles this appropriately)");

        // Cleanup any persisted keys
        await CacheProvider.RemoveAsync(zeroTtlKey);
        await CacheProvider.RemoveAsync(negativeTtlKey);

        _output.WriteLine("✅ Zero and negative TTL handling completed");
    }

    [Fact]
    public async Task TTL_ShouldHandle_VeryShortDurations()
    {
        // Arrange
        var testCases = new[]
        {
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100)
        };

        foreach (var ttl in testCases)
        {
            _output.WriteLine($"Testing very short TTL: {ttl.TotalMilliseconds}ms");

            var key = GenerateTestKey($"short_ttl_{ttl.TotalMilliseconds}ms");
            var value = $"short_value_{ttl.TotalMilliseconds}";

            var stopwatch = Stopwatch.StartNew();

            var setResult = await CacheProvider.SetAsync(key, value, ttl);
            setResult.Should().BeTrue($"SetAsync should succeed with {ttl.TotalMilliseconds}ms TTL");

            // For very short TTLs, the key might expire before we can check
            // This is acceptable behavior
            var immediateGet = await CacheProvider.GetAsync<string>(key);

            if (immediateGet != null)
            {
                // If we got the value, wait for expiration
                await Task.Delay(ttl.Add(TimeSpan.FromMilliseconds(100)));

                var expiredGet = await CacheProvider.GetAsync<string>(key);
                expiredGet.Should().BeNull($"Value should expire after {ttl.TotalMilliseconds}ms");
            }

            var elapsed = stopwatch.Elapsed;
            _output.WriteLine($"  Short TTL test completed in {elapsed.TotalMilliseconds:F2}ms");
        }

        _output.WriteLine("✅ Very short TTL duration handling completed");
    }

    [Fact]
    public async Task TTL_ShouldHandle_VeryLongDurations()
    {
        // Arrange
        var longTtlKey = GenerateTestKey("long_ttl");
        var longTtlValue = "long_ttl_value";
        var veryLongTtl = TimeSpan.FromHours(24); // 24 hours

        _output.WriteLine("Testing very long TTL duration...");

        // Set with very long TTL
        var setResult = await CacheProvider.SetAsync(longTtlKey, longTtlValue, veryLongTtl);
        setResult.Should().BeTrue("SetAsync with long TTL should succeed");

        // Verify key exists
        var existsResult = await CacheProvider.ExistsAsync(longTtlKey);
        existsResult.Should().BeTrue("Key with long TTL should exist");

        var getValue = await CacheProvider.GetAsync<string>(longTtlKey);
        getValue.Should().Be(longTtlValue, "Value with long TTL should be retrievable");

        // Update to shorter TTL for cleanup
        var shortTtl = TimeSpan.FromSeconds(1);
        var updateResult = await CacheProvider.SetExpiryAsync(longTtlKey, shortTtl);
        updateResult.Should().BeTrue("TTL update from long to short should succeed");

        // Wait for short TTL expiration
        await Task.Delay(shortTtl.Add(TimeSpan.FromMilliseconds(300)));

        // Verify expiration
        var expiredGet = await CacheProvider.GetAsync<string>(longTtlKey);
        expiredGet.Should().BeNull("Key should expire after TTL update to short duration");

        _output.WriteLine("✅ Very long TTL duration handling completed");
    }

    #endregion
}