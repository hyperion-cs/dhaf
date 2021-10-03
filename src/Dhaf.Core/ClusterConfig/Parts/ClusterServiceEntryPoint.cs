using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class ClusterServiceEntryPoint
    {
        [YamlMember(Alias = "name")]
        public string Id { get; set; }

        [YamlMember(Alias = "ip")]
        public string IP { get; set; }
    }
}
