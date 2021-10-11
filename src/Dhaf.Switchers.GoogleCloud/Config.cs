using Dhaf.Core;

namespace Dhaf.Switchers.GoogleCloud
{
    public class Config : ISwitcherConfig
    {
        public string ExtensionName => "google-cloud";

        public string Project { get; set; }
        public string MetadataKey { get; set; }
        public string CredentialsPath { get; set; }
    }
}
