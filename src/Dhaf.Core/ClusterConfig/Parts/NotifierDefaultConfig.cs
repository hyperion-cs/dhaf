using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class NotifierDefaultConfig : INotifierConfig
    {
        [YamlMember(Alias = "type")]
        public string ExtensionName { get; set; }
        public string Name { get => "aa"; }
    }
}
