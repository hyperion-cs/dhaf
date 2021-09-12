using Dhaf.Core;
using System.Collections.Generic;

namespace Dhaf.HealthCheckers.Web
{
    public class InternalConfig : IHealthCheckerInternalConfig
    {
        public string ExtensionName => "web";
        public int DefHttpPort { get; set; }
        public int DefHttpsPort { get; set; }
        public string HttpSchema { get; set; }
        public string HttpsSchema { get; set; }

        public string DefMethod { get; set; }
        public string DefPath { get; set; }
        public Dictionary<string, string> DefHeaders { get; set; }
        public bool DefFollowRedirects { get; set; }
        public int DefTimeout { get; set; }
        public int DefRetries { get; set; }

        public string DefExpectedCodes { get; set; }
        public string ExpectedCodesSeparator { get; set; }
        public string ExpectedCodesWildcard { get; set; }

        public string DefExpectedResponseBody { get; set; }
    }
}
