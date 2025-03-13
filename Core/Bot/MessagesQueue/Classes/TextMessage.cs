using Core.Bot.MessagesQueue.Interfaces;

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot.MessagesQueue.Classes {
    public class TextMessage(ChatId chatId, string text, ReplyMarkup? replyMarkup, ParseMode parseMode, bool disableNotification) : IMessageQueue {
        public ChatId ChatId { get; } = chatId;
        public string Text { get; } = text;

        public ReplyMarkup? ReplyMarkup { get; } = replyMarkup;
        public ParseMode ParseMode { get; } = parseMode;

        public bool DisableNotification { get; } = disableNotification;
    }
}
