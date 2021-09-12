using Dhaf.Core;

namespace Dhaf.Notifiers.Email
{
    public class Config : INotifierConfig
    {
        public string ExtensionName => "email";
        public string Name { get; set; }
    }
}
