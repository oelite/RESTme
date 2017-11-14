using System;


namespace OElite
{
    public class OEliteDbException : OEliteException
    {
        private const int ERROR_CODE = 1003;
        private const string ERROR_NAME = "GENERAL_DATA";
        public OEliteDbException() : base(ERROR_CODE, ERROR_NAME) { }
        public OEliteDbException(string msg) : base(msg, ERROR_CODE, ERROR_NAME, true) { }
        public OEliteDbException(string msg, int errorCode, string errorName) : base(msg, errorCode, errorName, true) { }
        public OEliteDbException(string msg, Exception innerException) : base(msg, innerException, ERROR_CODE, ERROR_NAME, true) { }
        public OEliteDbException(string msg, Exception innerException, int errorCode, string errorName) : base(msg, innerException, errorCode, errorName, true) { }

    }
}
