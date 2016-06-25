using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OElite.UnitTests
{
    public class EncryptHelperTests
    {
        [Fact]
        public void MD5Test()
        {
            string encrypted = EncryptHelper.MD5Encrypt("12345");
            Assert.True(encrypted == "827ccb0eea8a706c4c34a16891f84e7b");
        }
    
    }
}
