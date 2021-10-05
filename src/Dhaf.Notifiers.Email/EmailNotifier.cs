using Dhaf.Core;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dhaf.Notifiers.Email
{
    public class EmailNotifier : INotifier
    {
        private ILogger<INotifier> _logger;

        private Config _config;
        private InternalConfig _internalConfig;

        private SmtpClient _smtpClient;

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

            Sign = $"[{ExtensionName} ntf/{_config.Name}]";

            await AssertConfiguration();

            _logger.LogTrace($"{Sign} Using SMTP server {_config.Smtp.Server}:{_config.Smtp.Port}...");

            _smtpClient = new SmtpClient
            {
                Timeout = _internalConfig.Timeout
            };

            // Test connect
            try
            {
                var useSsl = _config.Smtp.Security == _internalConfig.SecuritySslFlag;
                _smtpClient.Connect(_config.Smtp.Server, _config.Smtp.Port, useSsl);

                if (_config.Smtp.Username is not null && _config.Smtp.Password is not null)
                {
                    _smtpClient.Authenticate(_config.Smtp.Username, _config.Smtp.Password);
                }

                _smtpClient.Disconnect(true);
            }
            catch (Exception e)
            {
                _logger.LogCritical($"{Sign} SMTP / {e.Message}");
                throw new ExtensionInitFailedException(Sign);
            }

            _logger.LogInformation($"{Sign} Init OK.");
        }

        public async Task Push(NotifierPushOptions options)
        {
            var messageData = await GetMessageData(options);
            var message = new MimeMessage
            {
                Subject = messageData.Subject,
                Body = new TextPart("html")
                {
                    Text = messageData.Body
                }
            };

            message.From.Add(new MailboxAddress(_internalConfig.SenderName, _config.From));
            message.To.Add(new MailboxAddress(_config.To, _config.To));

            var useSsl = _config.Smtp.Security == _internalConfig.SecuritySslFlag;

            _smtpClient.Connect(_config.Smtp.Server, _config.Smtp.Port, useSsl);

            if (_config.Smtp.Username is not null && _config.Smtp.Password is not null)
            {
                _smtpClient.Authenticate(_config.Smtp.Username, _config.Smtp.Password);
            }

            _smtpClient.Send(message);
            _smtpClient.Disconnect(true);
        }

        public async Task DhafNodeRoleChangedEventHandler(DhafNodeRole role) { }

        protected async Task<MessageData> GetMessageData(NotifierPushOptions options)
        {
            var messageData = new MessageData { Body = string.Empty };
            var timestamp = options.EventData.UtcTimestamp.ToString("F", CultureInfo.InvariantCulture);

            if (options.Event == NotifierEvent.ServiceUp
                 || options.Event == NotifierEvent.ServiceDown)
            {
                var eventData = (NotifierEventData.ServiceHealthChanged)options.EventData;

                var verb = options.Event == NotifierEvent.ServiceUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy ({WrapText("UP", "green")})"
                    : $"unhealthy ({WrapText("DOWN", "red")})";

                messageData.Subject
                    = $"Dhaf {options.Level}: SERVICE {verb} | {eventData.DhafCluster} | {eventData.Service}";

                messageData.Body = $"The service <b>{eventData.Service}</b> in dhaf cluster "
                 + $"<b>{eventData.DhafCluster}</b> is {longVerb}."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (options.Event == NotifierEvent.DhafNodeUp
                 || options.Event == NotifierEvent.DhafNodeDown)
            {
                var eventData = (NotifierEventData.DhafNodeHealthChanged)options.EventData;

                var verb = options.Event == NotifierEvent.DhafNodeUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy ({WrapText("UP", "green")})"
                    : $"unhealthy ({WrapText("DOWN", "red")})";

                messageData.Subject
                    = $"Dhaf {options.Level}: {verb} | {eventData.DhafCluster} | dhaf node <{eventData.NodeName}>";

                messageData.Body = $"The dhaf node <b>{eventData.NodeName}</b> in dhaf cluster "
                 + $"<b>{eventData.DhafCluster}</b> is {longVerb}."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (options.Event == NotifierEvent.Failover
                 || options.Event == NotifierEvent.Switchover
                 || options.Event == NotifierEvent.Switching)
            {
                var eventData = (NotifierEventData.CurrentEpChanged)options.EventData;
                var verb = options.Event.ToString().ToUpper();

                messageData.Subject
                    = $"Dhaf {options.Level}: {verb} | {eventData.DhafCluster} | {eventData.Service} | {eventData.FromEp} → {eventData.ToEp}";

                messageData.Body = $"There was a entry point {verb} from <b>{eventData.FromEp}</b> "
                 + $"to <b>{eventData.ToEp}</b> in the service <b>{eventData.Service}</b> "
                 + $"of dhaf cluster <b>{eventData.DhafCluster}</b>."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (options.Event == NotifierEvent.EpUp)
            {
                var eventData = (NotifierEventData.EpHealthChanged)options.EventData;
                messageData.Subject = $"Dhaf {options.Level}: UP | {eventData.DhafCluster} | {eventData.Service} | {eventData.EpName}";

                messageData.Body = $"The entry point <b>{eventData.EpName}</b> "
                                 + $"in the service <b>{eventData.Service}</b> of dhaf cluster "
                                 + $"<b>{eventData.DhafCluster}</b> is healthy ({WrapText("UP", "green")})."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (options.Event == NotifierEvent.EpDown)
            {
                var eventData = (NotifierEventData.EpHealthChanged)options.EventData;
                var firstReason = eventData.Reasons.FirstOrDefault();

                messageData.Subject
                    = $"Dhaf {options.Level}: DOWN | {eventData.DhafCluster} | {eventData.Service} | {eventData.EpName} | {firstReason}";

                var reasons = string.Join("; ", eventData.Reasons);

                messageData.Body = $"The entry point <b>{eventData.EpName}</b> in "
                                 + $"the service <b>{eventData.Service}</b> of dhaf cluster "
                                 + $"<b>{eventData.DhafCluster}</b> is unhealthy ({WrapText("DOWN", "red")})."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                                 + $"<br><b>Reason(s)</b>: {reasons}";
            }

            if (options.Event == NotifierEvent.SwitchoverPurged)
            {
                var eventData = (NotifierEventData.SwitchoverPurged)options.EventData;

                messageData.Subject
                    = $"Dhaf {options.Level}: SWITCHOVER PURGED | {eventData.DhafCluster} | {eventData.Service}";

                messageData.Body = $"The SWITCHOVER requirement to <b>{eventData.SwitchoverEp}</b> has been purged "
                                 + $"in the service <b>{eventData.Service}</b> of dhaf cluster <b>{eventData.DhafCluster}</b>."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (options.Event == NotifierEvent.DhafNewLeader)
            {
                var eventData = (NotifierEventData.DhafNewLeader)options.EventData;

                messageData.Subject
                    = $"Dhaf {options.Level}: NEW DHAF LEADER | {eventData.DhafCluster}";

                messageData.Body = $"Node <b>{eventData.Leader}</b> is the LEADER "
                                 + $"of the dhaf claster <b>{eventData.DhafCluster}</b> now."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>";
            }

            if (messageData.Subject is null)
            {
                var eventDataJson = JsonSerializer.Serialize(options.EventData, options.EventData.GetType());
                var defBody = $"An unexpected event occurred in the service <b>{options.EventData.Service ?? "none"}</b>"
                            + $" of dhaf cluster <b>{options.EventData.DhafCluster}</b>."
                            + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                            + $"<br>Event: <b>{options.Event}</b>"
                            + $"<br>EventData.Type: <b>{options.EventData.GetType()}</b>"
                            + $"<br>EventData.Json: <pre>{eventDataJson}</pre>";

                messageData.Subject = $"Dhaf {options.Level}: " +
                    $"Unknown event | {options.EventData.DhafCluster} | {options.EventData.Service ?? "none"}";

                messageData.Body = defBody;
            }

            messageData.Body += $"<br><i>* Notifier name: {_config.Name}</i>";
            return messageData;
        }

        protected static string WrapText(string str, string color, bool bold = true)
        {
            if (bold)
            {
                str = $"<b>{str}</b>";
            }

            return $"<span style=\"color: {color}\">{str}</span>";
        }

        protected async Task AssertConfiguration()
        {
            if (_config.Smtp is null)
            {
                _logger.LogCritical($"{Sign} The SMTP server settings are not set.");
                throw new ExtensionInitFailedException(Sign);
            }
        }
    }
}
