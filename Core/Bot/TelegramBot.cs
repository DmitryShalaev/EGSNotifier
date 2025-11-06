using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

using Core.DB;
using Core.DB.Entity;

using Microsoft.EntityFrameworkCore;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot {
    public static class TelegramBot {

        public static readonly TelegramBotClient botClient;

        public static readonly JsonSerializerOptions jsonSerializerOptions = new() {
            WriteIndented = true,
            IndentSize = 1,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
        };

        static TelegramBot() {
            if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TelegramBotToken")) ||
               string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TelegramBotConnectionString"))
              ) throw new NullReferenceException("Environment Variable is null");


            using(ScheduleDbContext dbContext = new())
                dbContext.Database.Migrate();

            botClient = new TelegramBotClient(Environment.GetEnvironmentVariable("TelegramBotToken")!);

            Task.Factory.StartNew(Jobs.Job.InitAsync, TaskCreationOptions.LongRunning);

            Console.WriteLine($"Запущен бот {botClient.GetMe().Result.FirstName}\n");
        }

        public static async Task UpdateAsync(Update update) {
            string msg = JsonSerializer.Serialize(update, jsonSerializerOptions) + "\n";
#if DEBUG
            Console.WriteLine(msg);
#endif

            try {
                using(ScheduleDbContext dbContext = new()) {
                    long? messageFrom = update.Message?.Chat.Id;

                    TelegramUser? user = await dbContext.TelegramUsers.FirstOrDefaultAsync(u => u.ChatID == messageFrom);

                    if(user is not null)
                        user.IsDeactivated = false;

                    Message? message;
                    switch(update.Type) {
                        case UpdateType.Message:
                            message = update.Message!;

                            if(user is null) {

                                user = new() {
                                    ChatID = (long)messageFrom!,
                                };

                                dbContext.TelegramUsers.Add(user);

                                await dbContext.SaveChangesAsync();
                            }

                            switch(message.Type) {
                                case MessageType.Text:
                                    if(message.Text! == "/start") {
                                        MessagesQueue.Message.SendTextMessage(chatId: messageFrom!, text: "✅ You have been successfully subscribed to Epic Games Store Free Games notifications!");

                                        DateTime today = DateTime.UtcNow;
                                        IQueryable<EGS> egs = dbContext.EGS.Where(i => i.StartDate <= today && today <= i.EndDate);
                                        foreach(EGS value in egs) {
                                            string caption = $"🎮 *{value.Title}*\n\n" +
                                            $"📖 *About:*\n" +
                                            $"{value.Description}\n\n" +
                                            $"💰 *Price:* {(value.OriginalPrice == "0" ? "Free" : $"~{value.OriginalPrice}~ → Free")} \n" +
                                            $"Start Date: {value.StartDate.ToUniversalTime():MMM dd 'at' hh:mm tt 'UTC'}\n" +
                                            $"End Date: {value.EndDate.ToUniversalTime():MMM dd 'at' hh:mm tt 'UTC'}";
                                            InlineKeyboardMarkup replyMarkup = new InlineKeyboardMarkup().AddButton(InlineKeyboardButton.WithUrl("Game Page", value.Page));

                                            MessagesQueue.Message.SendPhoto(
                                                chatId: messageFrom!,
                                                photo: value.Thumbnail,
                                                caption: SpecialCharacters.Escape(caption),
                                                parseMode: ParseMode.MarkdownV2,
                                                replyMarkup: replyMarkup
                                            );
                                        }
                                    }

                                    break;

                                default:
                                    break;
                            }

                            break;

                        case UpdateType.MyChatMember:
                            if(user is not null && update.MyChatMember!.NewChatMember.Status is ChatMemberStatus.Kicked or ChatMemberStatus.Left)
                                user.IsDeactivated = true;

                            break;

                        default:
                            break;
                    }

                    await dbContext.SaveChangesAsync();

                }
            } catch(Exception e) {
                await ErrorReport.Send(msg, e);
            }
        }
    }
}
