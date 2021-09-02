using System.Collections.Generic;

namespace Dhaf.Node
{
    public class ServiceStatus
    {
        public string Domain { get; set; }
        public string CurrentNcName { get; set; }
        public IEnumerable<ServiceNcStatus> NetworkConfigurations { get; set; }
    }

    public class ServiceNcStatus
    {
        public string Name { get; set; }
        public bool Healthy { get; set; }
    }
}
