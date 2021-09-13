using Dhaf.Core;

namespace Dhaf.Notifiers.Telegram
{
    public class Config : INotifierConfig
    {
        public string ExtensionName => "tg";
        public string Name { get; set; }
        public string JoinCode { get; set; }
        public string Token { get; set; }
    }
}
