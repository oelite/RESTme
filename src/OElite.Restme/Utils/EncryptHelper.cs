using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace OElite
{
    public class EncryptHelper
    {
        public static string MD5Encrypt(string valueString)
        {
            string ret = String.Empty;
            var md5Hasher = MD5.Create();
            byte[] data = System.Text.Encoding.ASCII.GetBytes(valueString);
            data = md5Hasher.ComputeHash(data);
            for (int i = 0; i < data.Length; i++)
            {
                ret += data[i].ToString("x2").ToLower();
            }
            return ret;

        }

        public static byte[] MD5Encrypt(byte[] sourceToEncrypt)
        {
            Byte[] encodedBytes;
            MD5 md5 = MD5.Create();
            encodedBytes = md5.ComputeHash(sourceToEncrypt);
            return encodedBytes;
        }

        public static byte[] GetDESKey()
        {
            var des = TripleDES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            des.GenerateKey();
            return des.Key;
        }

        public static string DESEncrypt(string pToEncrypt, byte[] sKey)
        {
            byte[] inputByteArray = UTF8Encoding.UTF8.GetBytes(pToEncrypt);
            return Convert.ToBase64String(DESEncrypt(inputByteArray, sKey));
        }

        public static byte[] DESEncrypt(byte[] pToEncrypt, byte[] sKey)
        {
            byte[] returnValue = null;
            using (var des = TripleDES.Create())
            {
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.PKCS7;
                byte[] inputByteArray = pToEncrypt;
                des.Key = sKey;
                des.IV = sKey;
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(inputByteArray, 0, inputByteArray.Length);
                        cs.FlushFinalBlock();
                    }
                    returnValue = ms.ToArray();
                }
            }
            return returnValue;
        }

        public static TripleDES GetDESEncryptor()
        {
            var des = TripleDES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            return des;
        }

    }
}
