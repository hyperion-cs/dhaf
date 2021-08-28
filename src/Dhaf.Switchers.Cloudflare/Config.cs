using Dhaf.Core;

namespace Dhaf.Switchers.Cloudflare
{
    public class Config : ISwitcherConfig
    {
        public string ExtensionName => "cloudflare";
        public string ApiToken { get; set; }
        public string Zone { get; set; }
    }
}
