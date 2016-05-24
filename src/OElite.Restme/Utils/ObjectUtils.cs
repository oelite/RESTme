using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OElite.Restme.Utils
{
    public static class ObjectUtils
    {
        public static T CreateObject<T>()
        {
            return Activator.CreateInstance<T>();
        }
        public static Random RandomWithUniqueSeed()
        {
            return new Random(int.Parse(Guid.NewGuid().ToString().Substring(0, 8), System.Globalization.NumberStyles.HexNumber));
        }
    }

}
