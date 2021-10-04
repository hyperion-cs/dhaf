using Dhaf.Core;

namespace Dhaf.Switchers.Foo
{
    // The dhaf extension system will automatically populate an
    // instance of this class from the internalConfiguration member of the extension.json file.
    public class InternalConfig : ISwitcherInternalConfig
    {
        public string ExtensionName => "foo";
    }
}
