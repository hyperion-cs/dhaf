using Dhaf.Core;

namespace Dhaf.Notifiers.Telegram
{
    public class InternalConfig : INotifierInternalConfig
    {
        public string ExtensionName => "tg";
        public int UpdatesPollingInterval { get; set; }
        public string StorageSubscribersPath { get; set; }
    }
}
