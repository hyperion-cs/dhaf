namespace Dhaf.Core.Tests
{
    public class SwitcherConfigMock : ISwitcherConfig
    {
        public string ExtensionName => "a";
    }

    public class SwitcherInternalConfigMock : ISwitcherInternalConfig
    {
        public string ExtensionName => "a";
    }

    public class HealthCheckerConfigMock : IHealthCheckerConfig
    {
        public string ExtensionName => "a";
    }

    public class HealthCheckerInternalConfigMock : IHealthCheckerInternalConfig
    {
        public string ExtensionName => "a";
    }

    public class NotifierConfigMock : INotifierConfig
    {
        public string ExtensionName => "a";

        public string Name => "a";
    }
    public class NotifierInternalConfigMock : INotifierInternalConfig
    {
        public string ExtensionName => "a";

        public string DefName => "a";
    }
}
