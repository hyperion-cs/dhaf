using Dhaf.Core;
using System.Collections.Generic;

namespace Dhaf.HealthCheckers.Http
{
    public class InternalConfig : IHealthCheckerInternalConfig
    {
        public string ExtensionName => "web";
        public int DefHttpPort { get; set; } = 80;
        public int DefHttpsPort { get; set; } = 443;
        public string HttpSchema { get; set; } = "http";
        public string HttpsSchema { get; set; } = "https";

        public string DefMethod { get; set; } = "GET";
        public string DefPath { get; set; } = "/";
        public Dictionary<string, string> DefHeaders { get; set; } = new Dictionary<string, string>();
        public bool DefFollowRedirects { get; set; } = false;
        public int DefTimeout { get; set; } = 5 * 1000;
        public int DefRetries { get; set; } = 2;

        public string DefExpectedCodes { get; set; } = "200";
        public string ExpectedCodesSeparator { get; set; } = ",";
        public string ExpectedCodesWildcard { get; set; } = "X";

        public string DefExpectedResponseBody { get; set; } = string.Empty;

    }
}
