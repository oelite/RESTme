# REST Http Client

RESTme (Restme) is a collectino of useful utility tools implemented in .NET Core aiming to increase productivity and keep code simplicity. Currently it includes: RESTful HTTP Client, Azure Storage Client, Redis Cache Client.

All tools are wrapped into single class **Restme()** to keep everything simple, the class will automatically identify whether it's used as HTTP Client, Azure Storage Client or Redis Cache Client.

### Features
* Implemented based on the latest .NET Core 1.0 (RC2)
* Simple methods and flexible calls
* Uses Newtonsoft JSON and allows custom serilization
* Async support
* HTTP Client:
    * Parameters get automatically converted into query string or post form fields
    * Supports GET, POST,  PUT, DELETE
    * Bearer Authentication and custom headers 
* Azure Storage Client:
    * Simplified call stack
* Redis Cache Client
    * Simplified call stack (Currently only support Azure Redis Cache, you can modify the source code to support other Redis Servers)

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
var result1 = rest.Get("/json/github.com");

//automatic Generic cast
var result2 = rest.Get<MyObject>("/json/github.com");
var resultAsync2 = await rest.GetAsync<<MyObjecT>("/json/github.com");

//add parameters (Parameters get automatically converted into query string or post form fields)
rest.add("q","github.com");
var result3 = rest.Get<MyObject>("/json");  
var resultAsync3 = await rest.GetAsync<MyObjecT>("/json");

//supports POST, DELETE, PUT etc.
var rest2 = new Restme(new Uri("http://example.com"));
rest2.Add("Username","abc@def.com");
rest2.Add("Birthday",DateTime.UtcNow);
rest2.Post<MyObject>("/someurl");

rest3.PostAsync<MyObject>("/asyncexample");


//supports direct object submission
var myObject = new MyObject()
{
    Username = "abc@def.com",
    Birthday = DateTime.UtcNow
};
var rest3 = new Restme(new Uri("http://example.com"));
rest3.Add(myObject);
rest3.Post<ExpectedResultObject>("/directObjectPost");


```

#### Use as a Azure Storage client
```csharp
//get blob stream directly
var blobStorageConnectionString = "{Your Storage Account Connection String}";
var rest = new Restme(blobStorageConnetionString);

rest.CreateAzureBlobContainerIfNotExists = true;  //do this only if you want to auto create the container

//NOTE 1: first segment of the path should always be your container name
//NOTE 3: Type T: if it is a type of Stream it will be stored as original Stream as Azure Blob, otherwise it is always saved into JSON format as Azure Blob
//NOTE 2: use a type of Stream if you want the original value retrieved from the blob
rest.Get<Stream>("/myContainer/myfilePath");  

rest.GetAsync<Stream>("/myContainer/myfilePath");

//NOTE: only blob items saved in JSON format is suppported
rest.Get<ObjectType>("/myContainer/myfileObjectInJSONFileFormat");
rest.GetAsync<ObjectType>("/myContainer/myfileObjectInJSONFileFormat");

```

### Use as a Redis client
```csharp
var redisConnectionString = "{Your Redis Cache Connection String}";
var rest = new Restme(redisConnectionString);

//get cache data (support Generic cast)
var cacheResult = rest.Get("home:testKey");
var cacheResult2 = rest.Get<bool>("home:testKey2");
var cacheResult3 = rest.Get<ObjectType>("home:testKey3");

//set cache data
rest.Post("home:testKey","value");
rest.Post<bool>("home:testKey2",true);

var myObject = new ObjectType();  //will be serialized into JSON format and stored as string on redis server
rest.Post<ObjectType>("home:testKey3", myObject);


```

### Contributions

This is a simple library just recently created, your contribution is welcomed.

### License
Released under [MIT License](http://choosealicense.com/licenses/mit).
