using System.Security.Cryptography;

namespace OElite
{
    public enum CryptFormat
    {
        MD5 = 0,
        DES = 1,
        RSA = 2,
        Unknown = -1
    }

    public static class CryptoHelper
    {
        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        public const int DefaultKeySize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        public const int DefaultDerivationIterations = 1000;

    }
}