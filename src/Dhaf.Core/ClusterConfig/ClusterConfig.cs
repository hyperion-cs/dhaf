using System.Collections.Generic;

namespace Dhaf.Core
{
    public class ClusterConfig
    {
        public ClusterDhafConfig Dhaf { get; set; }
        public ClusterEtcdConfig Etcd { get; set; }
        public List<ClusterServiceConfig> Services { get; set; } = new();
        public List<INotifierConfig> Notifiers { get; set; } = new();
    }
}
