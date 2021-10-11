namespace Dhaf.Core
{
    public class ClusterDhafConfig
    {
        public string ClusterName { get; set; }
        public string NodeName { get; set; }

        public int? HealthyNodeStatusTtl { get; set; }
        public int? HeartbeatInterval { get; set; }
        public int? TactInterval { get; set; }
        public int? TactPostSwitchDelay { get; set; }

        public ClusterDhafWebApiConfig WebApi { get; set; } = new();
    }

    public class ClusterDhafWebApiConfig
    {
        public string Host { get; set; }
        public int? Port { get; set; }
    }
}
