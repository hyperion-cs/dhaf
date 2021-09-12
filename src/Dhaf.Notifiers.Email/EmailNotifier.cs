using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Dhaf.Notifiers.Email
{
    public class EmailNotifier : INotifier
    {
        private ILogger<INotifier> _logger;

        private Config _config;
        private InternalConfig _internalConfig;

        public string ExtensionName => "email";

        public Type ConfigType => typeof(Config);
        public Type InternalConfigType => typeof(InternalConfig);

        public string Sign { get; protected set; }

        public async Task Init(NotifierInitOptions options)
        {
            Sign = $"[{ExtensionName} ntf]";

            _logger = options.Logger;
            _logger.LogTrace($"{Sign} Init process...");

            _config = (Config)options.Config;
            _internalConfig = (InternalConfig)options.InternalConfig;

            Sign = $"[{ExtensionName} ntf/{_config.Name ?? _internalConfig.DefName}]";

            _logger.LogInformation($"{Sign} Init OK.");

        }
    }
}
