using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OElite.Utils
{
    public class Restme
    {
        public static string MD5(string value)
        {
            return value.MD5Encrypt();
        }
    }
}
