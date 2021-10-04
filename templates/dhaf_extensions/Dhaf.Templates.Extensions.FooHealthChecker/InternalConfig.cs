using Dhaf.Core;

namespace Dhaf.HealthCheckers.Foo
{
    // The dhaf extension system will automatically populate an
    // instance of this class from the internalConfiguration member of the extension.json file.
    public class InternalConfig : IHealthCheckerInternalConfig
    {
        public string ExtensionName => "foo";
    }
}
