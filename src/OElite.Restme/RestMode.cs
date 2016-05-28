using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OElite
{
    public enum RestMode
    {
        HTTPClient = 0,
        AzureStorageClient = 10,
        RedisCacheClient = 20
    }
}
