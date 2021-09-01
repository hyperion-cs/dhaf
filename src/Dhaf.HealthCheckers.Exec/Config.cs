using Dhaf.Core;

namespace Dhaf.HealthCheckers.Exec
{
    public class Config : IHealthCheckerConfig
    {
        public string ExtensionName => "exec";

        public string Init { get; set; }
        public string Check { get; set; }
    }
}
