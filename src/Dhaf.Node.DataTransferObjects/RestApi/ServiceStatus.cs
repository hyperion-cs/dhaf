using System.Collections.Generic;

namespace Dhaf.Node
{
    public class ServiceStatus
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public string CurrentEntryPointName { get; set; }
        public IEnumerable<ServiceEntryPointStatus> EntryPoints { get; set; }
        public string SwitchoverRequirement { get; set; }
    }

    public class ServiceEntryPointStatus
    {
        public string Name { get; set; }
        public bool Healthy { get; set; }
        public int Priority { get; set; }
    }
}
