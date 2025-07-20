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
        public static string Md5Encrypt(string valueString)
        {
            var ret = string.Empty;
            var data = Encoding.ASCII.GetBytes(valueString);
            data = MD5.HashData(data);

            return data.Aggregate(ret, (current, t) => current + t.ToString("x2").ToLower());
        }

        public static byte[] Md5Encrypt(byte[] sourceToEncrypt)
        {
            var encodedBytes = MD5.HashData(sourceToEncrypt);
            return encodedBytes;
        }

        public static byte[] GetDesKey()
        {
            var des = TripleDES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            des.GenerateKey();
            return des.Key;
        }

        public static string DesEncrypt(string pToEncrypt, byte[] sKey)
        {
            var inputByteArray = Encoding.UTF8.GetBytes(pToEncrypt);
            return Convert.ToBase64String(DesEncrypt(inputByteArray, sKey));
        }

        public static byte[] DesEncrypt(byte[] pToEncrypt, byte[] sKey)
        {
            byte[] returnValue;
            using var des = TripleDES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            des.Key = sKey;
            des.IV = sKey;
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(pToEncrypt, 0, pToEncrypt.Length);
                cs.FlushFinalBlock();
            }

            returnValue = ms.ToArray();

            return returnValue;
        }


        public static TripleDES GetDesEncryptor()
        {
            var des = TripleDES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.PKCS7;
            return des;
        }

        public static string? GetHmacSha256(string key, string message)
        {
            var keyByte = Encoding.UTF8.GetBytes(key);
            using var hmacSha256 = new HMACSHA256(keyByte);
            hmacSha256.ComputeHash(Encoding.UTF8.GetBytes(message));

            return ByteToString(hmacSha256.Hash);
        }


        public static string AesEncrypt(string plainText, string passPhrase)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.
            var saltStringBytes = RandomNumberGenerator.GetBytes(16);
            var ivStringBytes = RandomNumberGenerator.GetBytes(16);
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using var password =
                new Rfc2898DeriveBytes(passPhrase, saltStringBytes, CryptoHelper.DefaultDerivationIterations);
            var keyBytes = password.GetBytes(CryptoHelper.DefaultKeySize / 8);
            using var symmetricKey = Aes.Create();
            symmetricKey.BlockSize = 128;
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

        public static string? ByteToString(IEnumerable<byte>? buff)
        {
            return buff?.Aggregate("", (current, t) => current + t.ToString("X2"));
        }
    }
}