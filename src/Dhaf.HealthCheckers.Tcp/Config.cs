using Dhaf.Core;

namespace Dhaf.HealthCheckers.Tcp
{
    public class Config : IHealthCheckerConfig
    {
        public string ExtensionName => "tcp";

        public int? Port { get; set; }
        public int? ReceiveTimeout { get; set; }
    }
}
