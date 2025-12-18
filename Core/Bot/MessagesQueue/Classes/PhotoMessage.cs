using Core.Bot.MessagesQueue.Interfaces;

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot.MessagesQueue.Classes {
    public class PhotoMessage(ChatId chatId, string photo, string? caption, ReplyMarkup? replyMarkup, string? path, bool hasSpoiler, bool disableNotification, ParseMode parseMode) : IMessageQueue {
        public ChatId ChatId { get; } = chatId;
        public string Photo { get; } = photo;
        public string? Caption { get; } = caption;
        public ReplyMarkup? ReplyMarkup { get; } = replyMarkup;
        public string? Path { get; } = path;
        public bool HasSpoiler { get; } = hasSpoiler;
        public bool DisableNotification { get; } = disableNotification;
        public ParseMode ParseMode { get; } = parseMode;
    }
}
