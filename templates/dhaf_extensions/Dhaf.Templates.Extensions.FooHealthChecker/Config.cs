using Dhaf.Core;

namespace Dhaf.HealthCheckers.Foo
{
    public class Config : IHealthCheckerConfig
    {
        public string ExtensionName => "foo";
    }
}
