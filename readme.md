# REST Http Client

RESTme (Restme) is a collectino of useful utility tools implemented in .NET Core aiming to increase productivity and keep code simplicity. Currently it includes: RESTful HTTP Client, Azure Storage Client, Redis Cache Client.

All tools are wrapped into single class **Restme()** to keep everything simple, the class will automatically identify whether it's used as HTTP Client, Azure Storage Client or Redis Cache Client.

### Features
* Implemented based on the latest .NET Core 1.0 (RC2)
* Simple methods and flexible calls
* Uses Newtonsoft JSON and allows custom serilization
* HTTP Client:
    * Parameters get automatically converted into query string or post form fields
    * Supports GET, POST,  PUT, DELETE
    * Bearer Authentication and custom headers 
* Azure Storage Client:
    * Simplified call stack
* Redis Cache Client
    * Simplified call stack

### Nuget Package
```csharp
    Install-Package OElite.Restme
```
Package available and released to Nuget:  [https://www.nuget.org/packages/OElite.Restme/](https://www.nuget.org/packages/OElite.Restme/)

### Usage

#### Use as a RESTful HTTP client

```csharp
//direct string JSON return
var rest = new Restme(new Uri("http://freegeoip.net"));
var result1 = rest.Get<string>("/json/github.com");

//automatic Generic cast
var result2 = rest.Get<MyObject>("/json/github.com");

//add parameters (Parameters get automatically converted into query string or post form fields)
rest.add("q","github.com");
var result3 = rest.Get<MyObject>("/json");

//supports POST, DELETE, PUT etc.
var rest2 = new Restme(new Uri("http://example.com"));
rest2.Add("Username","abc@def.com");
rest2.Add("Birthday",DateTime.UtcNow);
rest2.Post<MyObject>("/someurl");

//supports direct object submission
var myObject = new MyObject()
{
    Username = "abc@def.com",
    Birthday = DateTime.UtcNow
};
var rest3 = new Restme(new Uri("http://example.com"));
rest3.Add(myObject);


```

#### Use as a Azure Storage client
```csharp
//

```


### Contributions

This is a simple library just recently created, your contribution is welcomed.

### License
Released under [MIT License](http://choosealicense.com/licenses/mit).
