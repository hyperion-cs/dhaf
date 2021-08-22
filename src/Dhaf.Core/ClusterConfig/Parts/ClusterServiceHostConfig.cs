using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class ClusterServiceHostConfig
    {
        public string Name { get; set; }

        [YamlMember(Alias = "ip")]
        public string IP { get; set; }
    }
}
