using System.Collections.Generic;

namespace Dhaf.HealthCheckers.Web
{
    public enum DownReason
    {
        Timeout = 0, UnexpectedHttpCode = 1, UnexpectedResponseBody = 2
    }

    public static class DownReasonResolver
    {
        public static string Resolve(int code)
        {
            var x = (DownReason)code;

            if (!Map.ContainsKey(x))
            {
                return "Unexpected reason";
            }

            return Map[x];
        }

        private static Dictionary<DownReason, string> Map { get; set; } = new()
        {
            { DownReason.Timeout, "HTTP timeout occurred" },
            { DownReason.UnexpectedHttpCode, "Unexpected http code" },
            { DownReason.UnexpectedResponseBody, "Unexpected response body" }
        };
    }
}
