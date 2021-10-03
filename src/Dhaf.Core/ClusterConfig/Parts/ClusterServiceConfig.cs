using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Dhaf.Core
{
    public class ClusterServiceConfig
    {
        public string Name { get; set; }
        public string Domain { get; set; }

        public List<ClusterServiceEntryPoint> EntryPoints { get; set; } = new();
        public ISwitcherConfig Switcher { get; set; }
        public IHealthCheckerConfig HealthChecker { get; set; }
    }
}
