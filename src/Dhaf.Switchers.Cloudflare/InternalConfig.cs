using Dhaf.Core;
namespace Dhaf.Switchers.Cloudflare
{
    public class InternalConfig : ISwitcherInternalConfig
    {
        public string ExtensionName => "cloudflare";
        public string BaseUrl => "https://api.cloudflare.com/client/v4/";
        public int Jopa { get; set; }
    }
}
