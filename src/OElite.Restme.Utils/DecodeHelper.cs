using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace OElite
{
    public class DecodeHelper
    {

        public static string DESDecrypt(string pToDecrypt, byte[] sKey)
        {

            byte[] inputByteArray = Convert.FromBase64String(pToDecrypt);

            return UTF8Encoding.UTF8.GetString(DESDecrypt(inputByteArray, sKey)).Trim();
        }

        public static byte[] DESDecrypt(byte[] pToDecrypt, byte[] sKey)
        {
            using (TripleDES des = TripleDES.Create())
            {
                des.Padding = PaddingMode.PKCS7;
                des.Key = sKey;
                des.IV = sKey;
                byte[] returnValue = null;
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(pToDecrypt, 0, pToDecrypt.Length);
                        cs.FlushFinalBlock();
                    }
                    returnValue = ms.ToArray();
                }
                return returnValue;
            }
        }

    }
}
