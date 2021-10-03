using Dhaf.Core;
using System.Collections.Generic;

namespace Dhaf.Node
{
    public class DhafService
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public List<ClusterServiceEntryPoint> EntryPoints { get; set; }
        public ISwitcher Switcher { get; set; }
        public IHealthChecker HealthChecker { get; set; }

        /// <summary>
        /// The flag shows that all entry points is unhealthy.
        /// The service is DOWN and dhaf is physically unable to fix the situation on its own.
        /// </summary>
        public bool PanicMode { get; set; } = false;

        public string SwitchoverLastRequirementInStorage { get; set; } = null;

        public string CurrentEntryPointId { get; set; }

        public IEnumerable<EntryPointStatus> EntryPointStatuses { get; set; }
        public IEnumerable<EntryPointStatus> PreviousEntryPointStatuses { get; set; }  // For tracking changes.

        public DhafService(string name, string domain, List<ClusterServiceEntryPoint> entryPoints,
            ISwitcher switcher, IHealthChecker healthChecker)
        {
            Name = name;
            Domain = domain;
            EntryPoints = entryPoints;
            Switcher = switcher;
            HealthChecker = healthChecker;
        }
    }
}
