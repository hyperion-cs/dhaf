using System.Collections.Generic;

namespace Dhaf.Core
{
    public class ClusterServiceConfig
    {
        public string Domain { get; set; }

        public List<ClusterServiceHostConfig> Hosts { get; set; }
    }
}
