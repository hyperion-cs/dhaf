using Microsoft.Extensions.Logging;

namespace Dhaf.Core
{
    public class HealthCheckerInitOptions : IExtensionInitOptions
    {
        public ILogger<IHealthChecker> Logger { get; set; }
        public IHealthCheckerConfig Config { get; set; }
        public IHealthCheckerInternalConfig InternalConfig { get; set; }
        public ClusterServiceConfig ClusterServiceConfig { get; set; }
        public IExtensionStorageProvider Storage { get; set; }
    }
}
