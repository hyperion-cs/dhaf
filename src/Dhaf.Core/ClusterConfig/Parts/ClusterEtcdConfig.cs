namespace Dhaf.Core
{
    public class ClusterEtcdConfig
    {
        public string Hosts { get; set; }
        public int? LeaderKeyTtl { get; set; }
    }
}
