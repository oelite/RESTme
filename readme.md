# REST Http Client

RESTme (Restme) is a simple RESTful and HTTP client implemented in .NET Core

### Features
* Implemented based on the latest .NET Core 1.0 (RC2)
* Simple methods and flexible calls
* Uses Newtonsoft JSON and allows custom serilization
* GET, POST,  PUT, DELETE supported
* Parameters get automatically converted into query string or post form fields
* Bearer Authentication and custom headers 

### Usage

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


### Contributions

This is a simple library just recently created, your contribution is welcomed.

### License
Released under [MIT License](http://choosealicense.com/licenses/mit).
