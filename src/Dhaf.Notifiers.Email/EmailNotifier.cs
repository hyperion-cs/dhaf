﻿using Dhaf.Core;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
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

            Sign = $"[{ExtensionName} ntf/{_config.Name ?? _internalConfig.DefName}]";

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

        protected async Task<MessageData> GetMessageData(NotifierPushOptions options)
        {
            var messageData = new MessageData { Body = string.Empty };
            var timestamp = options.EventData.Timestamp.ToString("F");

            if (options.Event == NotifierEvent.ServiceUp
                 || options.Event == NotifierEvent.ServiceDown)
            {
                var eventData = (ServiceHealthChangedEventData)options.EventData;

                var verb = options.Event == NotifierEvent.ServiceUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy ({WrapText("UP", "green")})"
                    : $"unhealthy ({WrapText("DOWN", "red")})";

                messageData.Subject
                    = $"Dhaf {options.Level}: SERVICE {verb} | {eventData.DhafCluster}";

                messageData.Body = $"The service in dhaf cluster "
                 + $"<b>{eventData.DhafCluster}</b> is {longVerb}."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                 + $"<br>Domain: <b>{options.EventData.Domain}</b>";
            }

            if (options.Event == NotifierEvent.DhafNodeUp
                 || options.Event == NotifierEvent.DhafNodeDown)
            {
                var eventData = (DhafNodeHealthChangedEventData)options.EventData;

                var verb = options.Event == NotifierEvent.DhafNodeUp ? "UP" : "DOWN";
                var longVerb = verb == "UP" ? $"healthy ({WrapText("UP", "green")})"
                    : $"unhealthy ({WrapText("DOWN", "red")})";

                messageData.Subject
                    = $"Dhaf {options.Level}: {verb} | {eventData.DhafCluster} | dhaf node <{eventData.NodeName}>";

                messageData.Body = $"The dhaf node <b>{eventData.NodeName}</b> in dhaf cluster "
                 + $"<b>{eventData.DhafCluster}</b> is {longVerb}."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                 + $"<br>Domain: <b>{options.EventData.Domain}</b>";
            }

            if (options.Event == NotifierEvent.Failover
                 || options.Event == NotifierEvent.Switchover
                 || options.Event == NotifierEvent.Switching)
            {
                var eventData = (CurrentNcChangedEventData)options.EventData;
                var verb = options.Event.ToString().ToUpper();

                messageData.Subject
                    = $"Dhaf {options.Level}: {verb} | {eventData.DhafCluster} | {eventData.FromNc} → {eventData.ToNc}";

                messageData.Body = $"There was a network configuration {verb} from <b>{eventData.FromNc}</b> "
                 + $"to <b>{eventData.ToNc}</b> in dhaf cluster <b>{eventData.DhafCluster}</b>."
                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                 + $"<br>Domain: <b>{options.EventData.Domain}</b>";
            }

            if (options.Event == NotifierEvent.NcUp)
            {
                var eventData = (NcHealthChangedEventData)options.EventData;
                messageData.Subject = $"Dhaf {options.Level}: UP | {eventData.DhafCluster} | {eventData.NcName}";

                messageData.Body = $"The network configuration <b>{eventData.NcName}</b> in dhaf cluster "
                                 + $"<b>{eventData.DhafCluster}</b> is healthy ({WrapText("UP", "green")})."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                                 + $"<br>Domain: <b>{options.EventData.Domain}</b>";
            }

            if (options.Event == NotifierEvent.NcDown)
            {
                var eventData = (NcHealthChangedEventData)options.EventData;
                messageData.Subject
                    = $"Dhaf {options.Level}: DOWN | {eventData.DhafCluster} | {eventData.NcName} | {eventData.Reason}";

                messageData.Body = $"The network configuration <b>{eventData.NcName}</b> in dhaf cluster "
                                 + $"<b>{eventData.DhafCluster}</b> is unhealthy ({WrapText("DOWN", "red")})."
                                 + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                                 + $"<br>Domain: <b>{options.EventData.Domain}</b>"
                                 + $"<br><b>Reason</b>: {eventData.Reason}";
            }

            if (messageData.Subject is null)
            {
                var eventDataJson = JsonSerializer.Serialize(options.EventData, options.EventData.GetType());
                var defBody = $"An unexpected event occurred in dhaf cluster <b>{options.EventData.DhafCluster}</b>."
                            + $"<br>Timestamp (UTC): <b>{timestamp}</b>"
                            + $"<br>Domain: <b>{options.EventData.Domain}</b>"
                            + $"<br>Event: <b>{options.Event}</b>"
                            + $"<br>EventData.Type: <b>{options.EventData.GetType()}</b>"
                            + $"<br>EventData.Json: <pre>{eventDataJson}</pre>";

                messageData.Subject = $"Dhaf {options.Level}: Unknown event | {options.EventData.DhafCluster}";
                messageData.Body = defBody;
            }

            messageData.Body += $"<br><i>* Notifier name: {_config.Name ?? _internalConfig.DefName}</i>";
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
