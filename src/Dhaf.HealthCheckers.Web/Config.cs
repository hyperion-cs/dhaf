using Dhaf.Core;
using System.Collections.Generic;

namespace Dhaf.HealthCheckers.Web
{
    public class Config : IHealthCheckerConfig
    {
        public string ExtensionName => "web";
        public string Schema { get; set; }
        public string Method { get; set; }
        public int? Port { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public bool? FollowRedirects { get; set; }
        public int? Timeout { get; set; }
        public int? Retries { get; set; }
        public string ExpectedCodes { get; set; }
        public string ExpectedResponseBody { get; set; }
    }
}
