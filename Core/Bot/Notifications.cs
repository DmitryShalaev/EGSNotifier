using Core.Bot.MessagesQueue;
using Core.DB;
using Core.DB.Entity;

using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot {

    public static class Notifications {
        public static void UpdatedEGS(List<EGS> values) {
            using(ScheduleDbContext dbContext = new()) {

                var telegramUsers = dbContext.TelegramUsers.Where(u => !u.IsDeactivated).ToList();

                foreach(EGS value in values) {
                    string caption = $"🎮 *{value.Title}*\n\n" +
                                     $"📖 *About:*\n" +
                                     $"{value.Description}\n\n" +
                                     $"💰 *Price:* {(value.OriginalPrice == "0" ? "Free" : $"~{value.OriginalPrice}~ → Free")} \n" +
                                     $"Start Date: {value.StartDate.ToUniversalTime():MMM dd 'at' hh:mm tt 'UTC'}\n" +
                                     $"End Date: {value.EndDate.ToUniversalTime():MMM dd 'at' hh:mm tt 'UTC'}";
                    InlineKeyboardMarkup replyMarkup = new InlineKeyboardMarkup().AddButton(InlineKeyboardButton.WithUrl("Game Page", value.Page));

                    foreach(TelegramUser? user in telegramUsers) {
                        Message.SendSharedPhoto(chatId: user.ChatID, photo: value.Thumbnail, caption: SpecialCharacters.Escape(caption),
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2,
                            replyMarkup: replyMarkup);
                    }
                }
            }
        }
    }
}
