using Dhaf.Core;
using System.Collections.Generic;

namespace Dhaf.Node
{
    public class DhafService
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public List<ClusterServiceNetworkConfig> NetworkConfigurations { get; set; }
        public ISwitcher Switcher { get; set; }
        public IHealthChecker HealthChecker { get; set; }

        /// <summary>
        /// The flag shows that all NC is unhealthy.
        /// The service is DOWN and dhaf is physically unable to fix the situation on its own.
        /// </summary>
        public bool PanicMode { get; set; } = false;

        public string SwitchoverLastRequirementInStorage { get; set; } = null;

        public string CurrentNetworkConfigurationId { get; set; }

        public IEnumerable<NetworkConfigurationStatus> NetworkConfigurationStatuses { get; set; }
        public IEnumerable<NetworkConfigurationStatus> PreviousNetworkConfigurationStatuses { get; set; }  // For tracking changes.

        public DhafService(string name, string domain, List<ClusterServiceNetworkConfig> ncs,
            ISwitcher switcher, IHealthChecker healthChecker)
        {
            Name = name;
            Domain = domain;
            NetworkConfigurations = ncs;
            Switcher = switcher;
            HealthChecker = healthChecker;
        }
    }
}
