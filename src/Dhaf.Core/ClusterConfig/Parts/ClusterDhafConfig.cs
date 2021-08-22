namespace Dhaf.Core
{
    public class ClusterDhafConfig
    {
        public string ClusterName { get; set; }
        public string NodeName { get; set; }

        public int? HealthyNodeStatusTtl { get; set; }

        public int? HeartbeatInterval { get; set; }
        public int? FetchDhafNodeStatusesInterval { get; set; }
        public int? TactInterval { get; set; }
    }
}
