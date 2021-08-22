namespace Dhaf.Core
{
    public class SwitcherInitOptions
    {
        public ISwitcherConfig Config { get; set; }
        public ISwitcherInternalConfig InternalConfig { get; set; }
        public ClusterServiceConfig ClusterServiceConfig { get; set; }
    }
}
