using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class HealthCheckerDefaultConfig : IHealthCheckerConfig
    {
        [YamlMember(Alias = "type")]
        public string ExtensionName { get; set; }
    }
}
