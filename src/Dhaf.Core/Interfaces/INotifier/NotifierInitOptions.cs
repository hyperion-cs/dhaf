using Microsoft.Extensions.Logging;

namespace Dhaf.Core
{
    public class NotifierInitOptions
    {
        public ILogger<INotifier> Logger { get; set; }
        public INotifierConfig Config { get; set; }
        public INotifierInternalConfig InternalConfig { get; set; }
    }
}
