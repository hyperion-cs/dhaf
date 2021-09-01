using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class ClusterServiceConfig
    {
        public string Domain { get; set; }

        [YamlMember(Alias = "network-conf")]
        public List<ClusterServiceNetworkConfig> NetworkConfigurations { get; set; }
    }
}
