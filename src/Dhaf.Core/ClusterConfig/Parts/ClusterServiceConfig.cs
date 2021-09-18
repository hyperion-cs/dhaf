using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class ClusterServiceConfig
    {
        public string Name { get; set; }
        public string Domain { get; set; }

        [YamlMember(Alias = "network-conf")]
        public List<ClusterServiceNetworkConfig> NetworkConfigurations { get; set; }
        public ISwitcherConfig Switcher { get; set; }
        public IHealthCheckerConfig HealthChecker { get; set; }
    }
}
