# OElite.Restme.Utils

[![NuGet Version](https://img.shields.io/nuget/v/OElite.Restme.Utils.svg)](https://www.nuget.org/packages/OElite.Restme.Utils)
[![Target Framework](https://img.shields.io/badge/.NET-8%2C%209%2C%2010-blue)](https://dotnet.microsoft.com/)

Core utility library for the Restme framework, providing essential helper functions, extensions, and foundational types used across the OElite platform.

## 🚀 Key Features

### **Core Utilities**
- **String Processing**: Advanced string manipulation, validation, and formatting utilities
- **Encryption & Security**: MD5, DES/3DES encryption helpers with secure key generation
- **Date & Time**: Comprehensive date/time utilities and conversions
- **Type Conversion**: Robust type conversion and validation helpers
- **GUID Operations**: Enhanced GUID generation and manipulation

### **Web & HTTP Utilities**
- **HTTP Content Types**: Custom form-encoded and RESTful HTTP content handling
- **Web Crawling**: HTML parsing and web scraping utilities with HtmlAgilityPack integration
- **Query Processing**: URL query string and parameter handling

### **Foundation Types**
- **BaseEntity**: Core entity base class with common properties (Id, CreatedOnUtc, UpdatedOnUtc)
- **Entity Status**: Standardized entity status enumeration
- **Query Abstractions**: Base query classes for data access patterns
- **Exception Handling**: Custom OElite exception types with detailed error information

## Installation

```bash
dotnet add package OElite.Restme.Utils
```

## Quick Start

### String Validation and Processing
```csharp
using OElite;

// String validation
string validated = StringUtils.CanNotBeNullOrEmpty(userInput);

// String manipulation
string truncated = StringUtils.SubString(longText, 100, "...");
string cleaned = StringUtils.CleanHtmlTags(htmlContent);

// String formatting
string titleCase = StringUtils.ToTitleCase("hello world");
```

### Encryption and Security
```csharp
using OElite;

// MD5 hashing
string hash = EncryptHelper.Md5Encrypt("password123");

// DES encryption
byte[] key = EncryptHelper.GetDesKey();
string encrypted = EncryptHelper.DesEncrypt("sensitive data", key);
string decrypted = EncryptHelper.DesDecrypt(encrypted, key);

// TOTP generation
string totpCode = TotpEncrypt.GenerateTotp(secretKey);
bool isValid = TotpEncrypt.ValidateTotp(userCode, secretKey);
```

### Type Utilities
```csharp
using OElite;

// Numeric operations
decimal amount = NumericUtils.ParseDecimal(userInput);
bool isValidNumber = NumericUtils.IsValidCurrency(priceText);

// Boolean processing
bool result = BooleanUtils.ParseBoolean(checkboxValue);

// GUID operations
string shortGuid = GuidUtils.CreateShortGuid();
Guid parsed = GuidUtils.ParseGuid(guidString);
```

## Core Utilities

### String Processing

```csharp
// Validation
string validated = StringUtils.CanNotBeNullOrEmpty(userInput, trim: true);

// Safe substring operations
string excerpt = StringUtils.SubString(content, 150, "...");

// HTML processing
string plainText = StringUtils.StripHtmlTags(htmlContent);
string safeHtml = StringUtils.SanitizeHtml(userInput);

// Text formatting
string titleCase = StringUtils.ToTitleCase("hello world");
string slug = StringUtils.CreateSlug("Product Name Here!");
```

### Date and Time Processing

```csharp
// Current time operations
DateTime utcNow = DateTimeUtils.GetUtcNow();
DateTime localTime = DateTimeUtils.ConvertToLocal(utcTime, timezone);

// Date validation
bool isValid = DateTimeUtils.IsValidDateRange(startDate, endDate);
bool isFuture = DateTimeUtils.IsFutureDate(checkDate);

// Formatting
string displayDate = DateTimeUtils.FormatForDisplay(dateValue);
string isoString = DateTimeUtils.ToIsoString(dateTime);
```

### Numeric Utilities

```csharp
// Decimal operations
decimal amount = NumericUtils.ParseDecimal(userInput, defaultValue: 0);
bool isValidCurrency = NumericUtils.IsValidCurrency(priceText);

// Safe conversions
int safeInt = NumericUtils.ToInt32(stringValue);
double safeDouble = NumericUtils.ToDouble(numericString);

// Range validation
bool inRange = NumericUtils.IsInRange(value, min: 0, max: 1000);
```

### GUID Operations

```csharp
// GUID utilities
string shortGuid = GuidUtils.CreateShortGuid();
Guid newGuid = GuidUtils.CreateGuid();
Guid? parsed = GuidUtils.TryParseGuid(guidString);

// Validation
bool isValid = GuidUtils.IsValidGuid(inputString);
```

## Foundation Types

### BaseEntity

Core entity base class providing common functionality:

```csharp
using OElite.Restme.Utils;

public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Inherited properties:
    // - Id (string) - Primary key
    // - CreatedOnUtc (DateTime) - Creation timestamp
    // - UpdatedOnUtc (DateTime) - Last modification timestamp
}

// Using entity status
customer.Status = EntityStatus.Active;
```

### Entity Collections

```csharp
using OElite.Restme.Utils;

public class CustomerCollection : IEntityCollection<Customer>
{
    public List<Customer> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
```

## Security Features

### Encryption and Hashing

```csharp
// MD5 hashing
string hash = EncryptHelper.Md5Encrypt("password123");
byte[] hashBytes = EncryptHelper.Md5Encrypt(dataBytes);

// DES/3DES encryption
byte[] key = EncryptHelper.GetDesKey();
string encrypted = EncryptHelper.DesEncrypt("sensitive data", key);
string decrypted = EncryptHelper.DesDecrypt(encrypted, key);

// TOTP (Time-based One-Time Password)
string totpCode = TotpEncrypt.GenerateTotp(secretKey);
bool isValid = TotpEncrypt.ValidateTotp(userCode, secretKey, timeWindow: 30);
```

### Boolean Utilities

```csharp
// Parse various boolean representations
bool result = BooleanUtils.ParseBoolean("true");     // true
bool result = BooleanUtils.ParseBoolean("yes");      // true
bool result = BooleanUtils.ParseBoolean("1");        // true
bool result = BooleanUtils.ParseBoolean("on");       // true

// With default values
bool result = BooleanUtils.ParseBoolean(null, defaultValue: false);
```

## Web & HTTP Utilities

### HTTP Content Handling

```csharp
// Custom form-encoded content
var formContent = new OEliteFormUrlEncodedContent(parameters);

// RESTful HTTP content
var restContent = new OEliteRestfulHttpContent(data);

// HTTP response message extensions
HttpResponseMessage response = await httpClient.GetAsync(url);
string content = await response.ReadAsStringAsync();
```

### Web Crawling

```csharp
// HTML parsing with HtmlAgilityPack integration
var htmlDoc = WebCrawlerUtils.LoadHtmlDocument(url);
string extractedText = WebCrawlerUtils.ExtractText(htmlContent);
var links = WebCrawlerUtils.ExtractLinks(htmlDoc);

// URL and query processing
var parameters = QueryUtils.ParseQueryString(requestUrl);
string encoded = QueryUtils.BuildQueryString(parameterDict);
```

## File and Stream Utilities

```csharp
// File operations
byte[] fileBytes = FileUtils.ReadAllBytes(filePath);
string fileContent = FileUtils.ReadAllText(filePath);
bool exists = FileUtils.Exists(filePath);

// Stream processing
byte[] streamData = StreamUtils.ReadAllBytes(inputStream);
string streamText = StreamUtils.ReadAllText(inputStream);
```

## Error Handling

```csharp
using OElite.Restme.Utils;

try
{
    // Operation that might fail
    ProcessUserData(userData);
}
catch (OEliteException ex)
{
    // Handle OElite-specific exceptions
    RestmeLogger.LogError(ex.Message, ex);
}
```

## Integration

OElite.Restme.Utils serves as the foundation for other Restme packages:

- **OElite.Restme.MongoDb**: Uses base entities and utilities
- **OElite.Restme.Redis**: Leverages caching and serialization utilities
- **OElite.Restme.S3**: Uses file and stream utilities
- **OElite.Common**: Extends utilities with domain-specific functionality

## Requirements

- .NET 8.0, 9.0, or 10.0
- HtmlAgilityPack.NetCore 1.5.0.1+
- Newtonsoft.Json 13.0.3+
- Microsoft.Extensions.Logging.Abstractions 9.0.0+

## Thread Safety

Most utility methods are thread-safe. Thread utilities are provided for concurrent operations and background processing scenarios.

## License

Copyright © Phanes Technology Ltd. All rights reserved.