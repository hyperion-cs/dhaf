using Microsoft.Extensions.Logging;

namespace Dhaf.Core
{
    public class NotifierInitOptions : IExtensionInitOptions
    {
        public ILogger<INotifier> Logger { get; set; }
        public INotifierConfig Config { get; set; }
        public INotifierInternalConfig InternalConfig { get; set; }
        public IExtensionStorageProvider Storage { get; set; }
    }
}
