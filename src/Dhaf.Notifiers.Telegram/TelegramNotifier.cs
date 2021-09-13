using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Dhaf.Notifiers.Telegram
{
    public partial class TelegramNotifier : INotifier
    {
        private ILogger<INotifier> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private IExtensionStorageProvider _storage;

        private TelegramBotClient _botClient;

        public string ExtensionName => "tg";

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
            _storage = options.Storage;

            Sign = $"[{ExtensionName} ntf/{_config.Name ?? _internalConfig.DefName}]";

            if (_config.JoinCode is null)
            {
                _logger.LogCritical($"{Sign} Join code is not set.");
                throw new ExtensionInitFailedException(Sign);
            }

            if (_config.Token is null)
            {
                _logger.LogCritical($"{Sign} Telegram API auth token is not set.");
                throw new ExtensionInitFailedException(Sign);
            }

            _botClient = new TelegramBotClient(_config.Token);

            var me = await _botClient.GetMeAsync();
            _logger.LogTrace($"{Sign} Hello! I'm {me.FirstName} with id <{me.Id}>.");

            _handleUpdatesWithIntervalTask = HandleUpdatesWithInterval();

            _logger.LogInformation($"{Sign} Init OK.");
        }



        public Task Push(NotifierPushOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
