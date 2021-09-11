using System;

namespace Dhaf.Node
{
    public class RestApiException : Exception
    {
        public int Code { get; }
        public override string Message { get; }

        public RestApiException(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
