namespace Dhaf.Core
{
    public class ClusterEtcdConfig
    {
        public string Hosts { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }

        public int? LeaderKeyTtl { get; set; }
    }
}
