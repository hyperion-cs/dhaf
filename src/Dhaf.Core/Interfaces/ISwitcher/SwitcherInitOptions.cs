using Microsoft.Extensions.Logging;

namespace Dhaf.Core
{
    public class SwitcherInitOptions : IExtensionInitOptions
    {
        public ILogger<ISwitcher> Logger { get; set; }
        public ISwitcherConfig Config { get; set; }
        public ISwitcherInternalConfig InternalConfig { get; set; }
        public ClusterServiceConfig ClusterServiceConfig { get; set; }
        public IExtensionStorageProvider Storage { get; set; }
    }
}
