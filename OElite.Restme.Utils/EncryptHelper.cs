using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace OElite
{
    public partial class EncryptHelper
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

        public static string GetHMACSHA256(string key, string message)
        {
            var keyByte = Encoding.UTF8.GetBytes(key);
            using (var hmacSha256 = new HMACSHA256(keyByte))
            {
                hmacSha256.ComputeHash(Encoding.UTF8.GetBytes(message));

                return ByteToString(hmacSha256.Hash);
            }
        }

        public static string AesEncrypt(string plainText, string passPhrase)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = RandomNumberGenerator.GetBytes(32);
            var ivStringBytes = RandomNumberGenerator.GetBytes(32);
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using var password =
                new Rfc2898DeriveBytes(passPhrase, saltStringBytes, CryptoHelper.DefaultDerivationIterations);
            var keyBytes = password.GetBytes(CryptoHelper.DefaultKeySize / 8);
            using var symmetricKey = Aes.Create();
            symmetricKey.BlockSize = 256;
            symmetricKey.Mode = CipherMode.CBC;
            symmetricKey.Padding = PaddingMode.PKCS7;
            using var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes);
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            cryptoStream.FlushFinalBlock();
            // Create the final bytes as a concatenation of the random salt bytes, the random iv bytes and the cipher bytes.
            var cipherTextBytes = saltStringBytes;
            cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
            cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return Convert.ToBase64String(cipherTextBytes);
        }


        public static string ByteToString(IEnumerable<byte> buff)
        {
            return buff.Aggregate("", (current, t) => current + t.ToString("X2"));
        }

    }
}