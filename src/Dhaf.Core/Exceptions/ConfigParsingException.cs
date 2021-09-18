using System;

namespace Dhaf.Core
{
    public class ConfigParsingException : Exception
    {
        public int Code { get; }
        public override string Message { get; }

        public ConfigParsingException(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
