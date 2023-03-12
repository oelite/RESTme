using System;
using System.IO;
using System.Linq;
using System.Text;
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


        public static string AesDecrypt(string cipherText, string passPhrase)
        {
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [32 bytes of IV] + [n bytes of CipherText]
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            // Get the saltbytes by extracting the first 32 bytes from the supplied cipherText bytes.
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(CryptoHelper.DefaultKeySize / 8).ToArray();
            // Get the IV bytes by extracting the next 32 bytes from the supplied cipherText bytes.
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(CryptoHelper.DefaultKeySize / 8)
                .Take(CryptoHelper.DefaultKeySize / 8).ToArray();
            // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((CryptoHelper.DefaultKeySize / 8) * 2)
                .Take(cipherTextBytesWithSaltAndIv.Length - ((CryptoHelper.DefaultKeySize / 8) * 2)).ToArray();

            using var password =
                new Rfc2898DeriveBytes(passPhrase, saltStringBytes, CryptoHelper.DefaultDerivationIterations);
            var keyBytes = password.GetBytes(CryptoHelper.DefaultKeySize / 8);
            using var symmetricKey = Aes.Create();
            symmetricKey.BlockSize = 128;
            symmetricKey.Mode = CipherMode.CBC;
            symmetricKey.Padding = PaddingMode.PKCS7;
            using var decrypt = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes);
            using var memoryStream = new MemoryStream(cipherTextBytes);
            using var cryptoStream = new CryptoStream(memoryStream, decrypt, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }
    }
}