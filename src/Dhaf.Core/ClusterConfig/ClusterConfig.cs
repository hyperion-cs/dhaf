﻿namespace Dhaf.Core
{
    public class ClusterConfig
    {
        public ClusterDhafConfig Dhaf { get; set; }
        public ClusterEtcdConfig Etcd { get; set; }
        public ISwitcherConfig Switcher { get; set; }
        public ClusterServiceConfig Service { get; set; }
        public IHealthCheckerConfig HealthCheck { get; set; }
    }
}