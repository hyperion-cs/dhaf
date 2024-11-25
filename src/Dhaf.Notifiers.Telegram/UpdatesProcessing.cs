using Dhaf.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Dhaf.Notifiers.Telegram
{
    public partial class TelegramNotifier : INotifier
    {
        private static readonly Update[] EmptyUpdates = Array.Empty<Update>();
        private int? _messageOffset = null;
        private CancellationTokenSource _handleUpdatesWithIntervalCts = new();
        private Task _handleUpdatesWithIntervalTask;

        protected async Task HandleUpdatesWithInterval(CancellationToken cancellationToken)
        {
            var interval = _internalConfig.UpdatesPollingInterval;

            while (!cancellationToken.IsCancellationRequested)
            {
                var updates = await ReceiveUpdates();
                await HandleUpdates(updates);
                await Task.Delay(TimeSpan.FromSeconds(interval));
            }
        }

        protected async Task HandleUpdates(Update[] updates)
        {
            foreach (var update in updates)
            {
                if (update.Message.Type == MessageType.LeftChatMember)
                {
                    if (update.Message.LeftChatMember.Id == _botClient.BotId)
                    {
                        var chatId = update.Message.Chat.Id;
                        await DeleteSubscriber(chatId);

                        _logger.LogTrace($"{Sign} The bot was removed from chat with id <{chatId}>.");

                        continue;
                    }
                }

                if (update.Message.Type == MessageType.Text)
                {
                    var chatId = update.Message.Chat.Id;

                    try
                    {
                        if (update.Message.Text.StartsWith("/start"))
                        {
                            var subInStore = await GetSubscriberOfDefault(chatId);
                            if (subInStore.HasValue)
                            {
                                await _botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: "You are already subscribed to notifications."
                                    );

                                continue;
                            }

                            var parts = update.Message.Text.Split(' ');
                            if (parts.Length == 2)
                            {
                                var joinCode = parts[1];
                                if (joinCode == _config.JoinCode)
                                {
                                    await PutSubscriber(chatId, update.Message.Chat.Type.ToString());

                                    await _botClient.SendTextMessageAsync(
                                            chatId: chatId,
                                            text: "The join code is correct. You will now receive notifications from me."
                                        );

                                    _logger.LogInformation($"{Sign} Chat with id <{chatId}> has been added to receive notifications.");
                                }
                                else
                                {
                                    await _botClient.SendTextMessageAsync(
                                        chatId: chatId,
                                        text: "The join code is incorrect. Try again."
                                    );
                                }
                            }
                            else
                            {
                                await _botClient.SendTextMessageAsync(
                                       chatId: chatId,
                                       text: "Please use the following command format:\n```\n/start <join-code>\n```",
                                       parseMode: ParseMode.MarkdownV2
                                   );
                            }
                        }
                        else
                        {
                            var subInStore = await GetSubscriberOfDefault(chatId);
                            if (subInStore.HasValue)
                            {
                                await _botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: "I don't understand you. You're subscribed to notifications from me, so you can relax for now."
                                );
                            }
                            else
                            {
                                await _botClient.SendTextMessageAsync(
                                    chatId: chatId,
                                    text: "I don't understand you\\. By the way, you're not subscribed to notifications from me\\. I recommend that you do so using the following command:\n```\n/start <join-code>\n```",
                                    parseMode: ParseMode.MarkdownV2
                                );
                            }
                        }
                    }
                    catch (ApiRequestException e)
                    {
                        await ProcessPossibleUnavailableSubscriber(e, chatId);
                    }
                    catch { }
                }
            }
        }

        protected async Task<Update[]> ReceiveUpdates()
        {
            var allowedUpdates = new List<UpdateType>()
            {
                UpdateType.Message
            };

            var updates = EmptyUpdates;

            var request = new GetUpdatesRequest
            {
                Offset = _messageOffset ?? 0,
                AllowedUpdates = allowedUpdates,
            };

            try
            {
                updates = await _botClient.MakeRequestAsync(request);
            }
            catch { }

            if (updates.Any())
            {
                var lastUpdateId = updates.Max(x => x.Id);
                _messageOffset = lastUpdateId + 1;
            }

            return updates;
        }
    }
}
