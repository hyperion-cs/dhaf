using Dhaf.Core;

namespace Dhaf.Notifiers.Email
{
    public class InternalConfig : INotifierInternalConfig
    {
        public string ExtensionName => "email";
        public string DefName { get; set; }
        public string SenderName { get; set; }
        public string SecuritySslFlag { get; set; }
        public int Timeout { get; set; }
    }
}
