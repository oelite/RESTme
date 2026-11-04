using System;
using System.Collections.Generic;
using OElite;
using Xunit;

namespace OElite.Restme.UnitTests
{
    /// <summary>
    /// Pure unit tests for OElite.Restme.Utils string/guid helpers.
    /// No persistence, no network, no shared-stack dependencies — safe for MR pipelines.
    /// </summary>
    public class StringUtilsTests
    {
        [Theory]
        [InlineData("  hello  ", "hello")]
        [InlineData("hello", "hello")]
        public void CanNotBeNullOrEmpty_ValidInput_ReturnsTrimmedValue(string input, string expected)
        {
            var result = StringUtils.CanNotBeNullOrEmpty(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CanNotBeNullOrEmpty_ObjectOverload_ReturnsTrimmedValue()
        {
            var result = StringUtils.CanNotBeNullOrEmpty((object)"  value  ");
            Assert.Equal("value", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CanNotBeNullOrEmpty_InvalidInput_ThrowsOEliteException(string? input)
        {
            Assert.Throws<OEliteException>(() => StringUtils.CanNotBeNullOrEmpty(input!));
        }

        [Fact]
        public void SubString_ShorterThanLimit_ReturnsOriginal()
        {
            var result = StringUtils.SubString("short", 10, "...");
            Assert.Equal("short", result);
        }

        [Fact]
        public void SubString_LongerThanLimit_TruncatesAndAppends()
        {
            var result = StringUtils.SubString("abcdefghijkl", 5, "...");
            Assert.Equal("abcde...", result);
        }

        [Fact]
        public void GetStringValueOrEmpty_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, StringUtils.GetStringValueOrEmpty(null));
        }

        [Fact]
        public void GetStringValueOrEmpty_String_ReturnsValue()
        {
            Assert.Equal("abc", StringUtils.GetStringValueOrEmpty("abc"));
        }

        [Fact]
        public void GenerateRandomId_IsNonEmpty()
        {
            var id = StringUtils.GenerateRandomId();
            Assert.False(string.IsNullOrEmpty(id));
        }

        [Fact]
        public void JsonSerialize_RoundTrip_PreservesValues()
        {
            var payload = new Dictionary<string, object?> { ["name"] = "restme", ["n"] = 42 };
            var json = StringUtils.JsonSerialize(payload);
            Assert.Contains("\"name\"", json);
            Assert.Contains("restme", json);
        }
    }

    public class GuidUtilsTests
    {
        [Fact]
        public void GetGuidOrEmpty_Null_ReturnsEmpty()
        {
            Assert.Equal(Guid.Empty, GuidUtils.GetGuidOrEmpty(null));
        }

        [Fact]
        public void GetGuidOrEmpty_ValidString_ReturnsGuid()
        {
            var expected = Guid.NewGuid();
            Assert.Equal(expected, GuidUtils.GetGuidOrEmpty(expected.ToString()));
        }

        [Fact]
        public void GetGuidOrEmpty_InvalidString_ReturnsEmpty()
        {
            Assert.Equal(Guid.Empty, GuidUtils.GetGuidOrEmpty("not-a-guid"));
        }

        [Fact]
        public void GetGuidOrEmpty_GuidInstance_ReturnsSameGuid()
        {
            var expected = Guid.NewGuid();
            Assert.Equal(expected, GuidUtils.GetGuidOrEmpty(expected));
        }

        [Fact]
        public void IsNullOrEmpty_EmptyGuid_ReturnsTrue()
        {
            Assert.True(Guid.Empty.IsNullOrEmpty());
        }

        [Fact]
        public void IsNotNullOrEmpty_NewGuid_ReturnsTrue()
        {
            Assert.True(Guid.NewGuid().IsNotNullOrEmpty());
        }
    }
}
