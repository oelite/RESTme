using System;

namespace OElite
{

    public class OEliteException : Exception
    {
        //protected static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public int OEliteErrorCode { get; set; }
        public string OEliteErrorName { get; set; }
        public OEliteException(bool createlog = false) : base()
        {
            OEliteErrorCode = 1001;
            OEliteErrorName = "GENERAL";
            //if (createlog)
            //    logger.Error("[OElite Exception]", this);
        }
        public OEliteException(int errorCode, string errorName, bool createlog = false) : base()
        {
            OEliteErrorCode = errorCode;
            OEliteErrorName = errorName;
            //if (createlog)
            //    logger.Error("[OElite Exception]", this);
        }
        public OEliteException(string msg, bool createlog = false) : base(msg)
        {
            OEliteErrorCode = 1001; OEliteErrorName = "GENERAL";
            //if (createlog)
            //    logger.Error("[OElite Exception: " + msg + " ]", this);
        }
        public OEliteException(string msg, int errorCode, string errorName, bool createlog = false) : base(msg)
        {
            OEliteErrorCode = errorCode; OEliteErrorName = errorName;
            //if (createlog)
            //    logger.Error(msg, this);
        }
        public OEliteException(string msg, Exception innerException, bool createlog = false) : base(msg, innerException)
        {
            OEliteErrorCode = 1001; OEliteErrorName = "GENERAL";
            //if (createlog)
            //    logger.Error(innerException, msg);
        }
        public OEliteException(string msg, Exception innerException, int errorCode, string errorName, bool createlog = false) : base(msg, innerException)
        {
            OEliteErrorCode = errorCode; OEliteErrorName = errorName;
            //if (createlog)
            //    logger.Error(innerException, "[OElite Exception: " + msg + " ]");
        }
    }
}
