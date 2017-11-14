using System;


namespace OElite
{
    public class OEliteSecurityException : OEliteException
    {
        private const int ERROR_CODE = 1002;
        private const string ERROR_NAME = "GENERAL_SECURITY";

        public OEliteSecurityException() : base("permission denied.", ERROR_CODE, ERROR_NAME) { }
        public OEliteSecurityException(int errorCode, string errorName) : base("permission denied.", errorCode, errorName) { }
        public OEliteSecurityException(string msg) : base(msg, ERROR_CODE, ERROR_NAME) { }
        public OEliteSecurityException(string msg, int errorCode, string errorName) : base(msg, errorCode, errorName) { }
        public OEliteSecurityException(string msg, Exception innerException) : base(msg, innerException, ERROR_CODE, ERROR_NAME) { }
        public OEliteSecurityException(string msg, Exception innerException, int errorCode, string errorName) : base(msg, innerException, errorCode, errorName) { }

    }
}
