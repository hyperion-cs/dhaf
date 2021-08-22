using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class SwitcherDefaultConfig : ISwitcherConfig
    {
        [YamlMember(Alias = "type")]
        public string ExtensionName { get; set; }
    }
}
