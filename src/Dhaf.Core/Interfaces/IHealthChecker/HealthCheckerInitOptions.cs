namespace Dhaf.Core
{
    public class HealthCheckerInitOptions
    {
        public IHealthCheckerConfig Config { get; set; }
        public IHealthCheckerInternalConfig InternalConfig { get; set; }
        public ClusterServiceConfig ClusterServiceConfig { get; set; }
    }
}
