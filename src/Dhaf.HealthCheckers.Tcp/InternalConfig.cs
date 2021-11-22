using Dhaf.Core;

namespace Dhaf.HealthCheckers.Tcp
{
    public class InternalConfig : IHealthCheckerInternalConfig
    {
        public string ExtensionName => "tcp";

        public int DefReceiveTimeout { get; set; }
        public int MinReceiveTimeout { get; set; }
        public int MaxReceiveTimeout { get; set; }
    }
}
