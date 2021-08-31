using Microsoft.Extensions.Logging;

namespace Dhaf.Core
{
    public class SwitcherInitOptions
    {
        public ILogger<ISwitcher> Logger { get; set; }
        public ISwitcherConfig Config { get; set; }
        public ISwitcherInternalConfig InternalConfig { get; set; }
        public ClusterServiceConfig ClusterServiceConfig { get; set; }
    }
}
