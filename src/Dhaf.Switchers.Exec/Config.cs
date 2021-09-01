using Dhaf.Core;

namespace Dhaf.Switchers.Exec
{
    public class Config : ISwitcherConfig
    {
        public string ExtensionName => "exec";

        public string Init { get; set; }
        public string Switch { get; set; }
    }
}
