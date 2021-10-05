using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace Dhaf.Notifiers.Telegram
{
    public partial class TelegramNotifier : INotifier
    {
        private ILogger<INotifier> _logger;

        private Config _config;
        private InternalConfig _internalConfig;
        private IExtensionStorageProvider _storage;

        private TelegramBotClient _botClient;

        private static List<Func<ApiRequestException, bool>> _unavailableSubResponses = new()
        {
            (e) => e.ErrorCode == 400 && e.Message.Contains("chat not found"),
            (e) => e.ErrorCode == 403 && e.Message.Contains("bot was kicked")
        };

        protected DhafNodeRole _currentDhafNodeRole = DhafNodeRole.Follower;

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

            Sign = $"[{ExtensionName} ntf/{_config.Name}]";

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

            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Push(NotifierPushOptions options)
        {
            var subs = await GetSubscribers();
            var message = await GetMessage(options);

            foreach (var sub in subs)
            {
                try
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: sub,
                        text: message,
                        parseMode: ParseMode.MarkdownV2
                    );
                }
                catch (ApiRequestException e)
                {
                    await ProcessPossibleUnavailableSubscriber(e, sub);
                }
                catch { }
            }
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role)
        {
            _currentDhafNodeRole = role;

            if (!_handleUpdatesWithIntervalCts.IsCancellationRequested)
            {
                _handleUpdatesWithIntervalCts.Cancel();
            }

            if (_handleUpdatesWithIntervalTask is not null)
            {
                await _handleUpdatesWithIntervalTask;
            }

            if (_currentDhafNodeRole == DhafNodeRole.Leader)
            {
                _handleUpdatesWithIntervalCts = new();
                _handleUpdatesWithIntervalTask = HandleUpdatesWithInterval(_handleUpdatesWithIntervalCts.Token);
            }
        }

        protected async Task ProcessPossibleUnavailableSubscriber(ApiRequestException e, long sub)
        {
            if (_unavailableSubResponses.Any(x => x(e)))
            {
                await DeleteSubscriber(sub);

                _logger.LogWarning($"{Sign} Chat with {sub} is unavailable. It will be removed from the list of notification subscribers.");
            }
            else
            {
                _logger.LogError($"{Sign} Error {e.ErrorCode}: {e.Message}");
            }
        }

        protected async Task<string> GetMessage(NotifierPushOptions options)
        {
            var message = string.Empty;

            var dhafCluster = MdEscape(options.EventData.DhafCluster);
            var timestamp = MdEscape(options.EventData.UtcTimestamp.ToString("F", CultureInfo.InvariantCulture));
            var service = MdEscape(options.EventData.Service ?? "none");

            if (options.Event == NotifierEvent.ServiceUp
                 || options.Event == NotifierEvent.ServiceDown)
            {
                var verb = options.Event == NotifierEvent.ServiceUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy \\(*UP*\\)" : $"unhealthy \\(*DOWN*\\)";

                message = $"The service *{service}* in dhaf cluster "
                 + $"*{dhafCluster}* is {longVerb}\\."
                 + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (options.Event == NotifierEvent.DhafNodeUp
                 || options.Event == NotifierEvent.DhafNodeDown)
            {
                var eventData = (NotifierEventData.DhafNodeHealthChanged)options.EventData;

                var verb = options.Event == NotifierEvent.DhafNodeUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy \\(*UP*\\)" : $"unhealthy \\(*DOWN*\\)";
                var nodeName = MdEscape(eventData.NodeName);

                message = $"The dhaf node *{nodeName}* in dhaf cluster "
                 + $"*{dhafCluster}* is {longVerb}\\."
                 + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (options.Event == NotifierEvent.Failover
                 || options.Event == NotifierEvent.Switchover
                 || options.Event == NotifierEvent.Switching)
            {
                var eventData = (NotifierEventData.CurrentEpChanged)options.EventData;
                var verb = options.Event.ToString().ToUpper();

                var fromNc = MdEscape(eventData.FromEp);
                var toNc = MdEscape(eventData.ToEp);

                message = $"There was a entry point {verb} from *{fromNc}* "
                 + $"to *{toNc}* in the service *{service}* of dhaf cluster *{dhafCluster}*\\."
                 + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (options.Event == NotifierEvent.EpUp)
            {
                var eventData = (NotifierEventData.EpHealthChanged)options.EventData;
                var entryPointName = MdEscape(eventData.EpName);

                message = $"The entry point *{entryPointName}* in the service *{service}* of dhaf cluster "
                                 + $"*{dhafCluster}* is healthy \\(*UP*\\)\\."
                                 + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (options.Event == NotifierEvent.EpDown)
            {
                var eventData = (NotifierEventData.EpHealthChanged)options.EventData;
                var entryPointName = MdEscape(eventData.EpName);
                var reasons = MdEscape(string.Join("; ", eventData.Reasons));

                message = $"The entry point *{entryPointName}* in the service *{service}* of dhaf cluster "
                                 + $"*{dhafCluster}* is unhealthy \\(*DOWN*\\)\\."
                                 + $"\n\nTimestamp \\(UTC\\): *{timestamp}*"
                                 + $"\n*Reason\\(s\\)*: {reasons}";
            }

            if (options.Event == NotifierEvent.SwitchoverPurged)
            {
                var eventData = (NotifierEventData.SwitchoverPurged)options.EventData;
                var switchoverEntryPoint = MdEscape(eventData.SwitchoverEp);

                message = $"The SWITCHOVER requirement to *{switchoverEntryPoint}* has been purged "
                        + $"in the service *{service}* of dhaf cluster *{dhafCluster}*\\."
                        + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (options.Event == NotifierEvent.DhafNewLeader)
            {
                var eventData = (NotifierEventData.DhafNewLeader)options.EventData;
                var leader = MdEscape(eventData.Leader);

                message = $"Node *{leader}* is the LEADER "
                        + $"of the dhaf claster *{dhafCluster}*\\ now\\."
                        + $"\n\nTimestamp \\(UTC\\): *{timestamp}*";
            }

            if (message == string.Empty)
            {
                var eventDataJson = JsonSerializer.Serialize(options.EventData, options.EventData.GetType());
                var eventDataJsonEscaped = MdEscape(eventDataJson);
                var eventDataType = MdEscape(options.EventData.GetType().ToString());

                message = $"An unexpected event occurred in the service *{service}* of dhaf cluster *{dhafCluster}*\\."
                            + $"\n\nTimestamp \\(UTC\\): *{timestamp}*"
                            + $"\nEvent: *{options.Event}*"
                            + $"\nEventData\\.Type: *{eventDataType}*"
                            + $"\nEventData\\.Json:\n```\n{eventDataJsonEscaped}\n```";
            }

            message = $"*{options.Level.ToString().ToUpper()}*:\n"
                + message
                + $"\n\n_\\* Notifier name: {MdEscape(_config.Name)}_";

            return message;
        }

        protected static string MdEscape(string str)
        {
            var escapedChars = new char[] {
                '_', '*', '[', ']', '(', ')', '~', '`',
                '>', '#', '+', '-', '=', '|', '{', '}', '.', '!', '\\' };

            var escapeChar = '\\';
            var result = new StringBuilder();

            foreach (var _char in str)
            {
                if (escapedChars.Contains(_char))
                {
                    result.Append($"{escapeChar}{_char}");
                    continue;
                }

                result.Append(_char);
            }

            return result.ToString();
        }
    }
}
