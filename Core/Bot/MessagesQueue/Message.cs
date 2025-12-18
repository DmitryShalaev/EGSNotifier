using System.Collections.Concurrent;

using Core.Bot.MessagesQueue.Classes;
using Core.Bot.MessagesQueue.Interfaces;
using Core.DB;

using Microsoft.EntityFrameworkCore;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Bot.MessagesQueue {

    public static class Message {
        private static ITelegramBotClient BotClient => TelegramBot.botClient;

        private static readonly ConcurrentDictionary<ChatId, int> previewsMessage = new();

        // Очереди сообщений для каждого пользователя
        private static readonly ConcurrentDictionary<ChatId, (SemaphoreSlim semaphore, ConcurrentQueue<IMessageQueue> queue)> userQueues = new();

        private static readonly ConcurrentQueue<IMessageQueue> sharedQueue = new();

        // Переменные для глобального ограничения количества сообщений
        private static readonly SemaphoreSlim globalLock = new(1, 1);

        private const int globalRateLimit = 10; // Максимальное количество сообщений в секунду
        private static int globalMessageCount = 0;

        private static DateTime lastGlobalResetTime = DateTime.UtcNow;
        private static readonly TimeSpan globalRateLimitInterval = TimeSpan.FromSeconds(1); // Интервал времени для лимита

        // Дополнительный словарь для контроля всплесков
        private static readonly ConcurrentDictionary<ChatId, (int burstCount, DateTime lastMessageTime)> userBurstControl = new();

        // Настройки всплесков
        private static readonly int burstLimit = 4; // Максимальное количество сообщений в всплеске
        private static readonly TimeSpan burstInterval = TimeSpan.FromSeconds(1); // Интервал времени для всплеска

        #region Messag

        public static void SendTextMessage(ChatId chatId, string text, ReplyMarkup? replyMarkup = null, ParseMode parseMode = ParseMode.None, bool disableNotification = false) {
            var message = new TextMessage(chatId, text, replyMarkup, parseMode, disableNotification);

            AddMessageToQueue(message);
        }

        public static void SendPhoto(ChatId chatId, string photo, string? caption = null, ReplyMarkup? replyMarkup = null, string? path = null, bool hasSpoiler = false, bool disableNotification = false, ParseMode parseMode = ParseMode.None) {
            var message = new PhotoMessage(chatId, photo, caption, replyMarkup, path, hasSpoiler, disableNotification, parseMode);

            AddMessageToQueue(message);
        }

        public static void SendSharedPhoto(ChatId chatId, string photo, string? caption = null, ReplyMarkup? replyMarkup = null, string? path = null, bool hasSpoiler = false, bool disableNotification = false, ParseMode parseMode = ParseMode.None) {
            var message = new PhotoMessage(chatId, photo, caption, replyMarkup, path, hasSpoiler, disableNotification, parseMode);

            AddMessageToSharedQueue(message);
        }

        #endregion

        private static void AddMessageToQueue(IMessageQueue message) {
            (SemaphoreSlim semaphore, ConcurrentQueue<IMessageQueue> queue) user = userQueues.GetOrAdd(message.ChatId, _ => (new SemaphoreSlim(1, 1), new ConcurrentQueue<IMessageQueue>()));

            user.queue.Enqueue(message);

            // Запускаем обработку очереди 
            if(user.semaphore.CurrentCount > 0) {
                _ = Task.Run(() => ProcessQueue(user));
            }
        }

        private static void AddMessageToSharedQueue(PhotoMessage message) {
            sharedQueue.Enqueue(message);
            Console.WriteLine($"AddMessageToSharedQueue: {sharedQueue.Count}");
            // Запускаем обработку очереди массовой рассылки
            if(sharedQueue.Count == 1) {
                Console.WriteLine("Start ProcessSharedQueue");
                _ = Task.Run(ProcessSharedQueue);
            }
        }

        private static async Task ProcessSharedQueue() {
            while(!sharedQueue.IsEmpty) {
                // Проверяем, есть ли сообщения в пользовательских очередях
                bool isUserQueueEmpty = userQueues.All(u => u.Value.queue.IsEmpty);

                if(isUserQueueEmpty) {
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    if(sharedQueue.TryDequeue(out IMessageQueue? message)) {
                        Console.WriteLine($"Process {sharedQueue.Count}");

                        // Отправляем сообщение
                        await SendMessageAsync(message);
                    }
                } else {
                    // Если есть сообщения в пользовательских очередях, ждем и проверяем снова
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        private static async Task ProcessQueue((SemaphoreSlim semaphore, ConcurrentQueue<IMessageQueue> queue) user) {
            await user.semaphore.WaitAsync();
            try {

                while(!user.queue.IsEmpty) {
                    if(user.queue.TryDequeue(out IMessageQueue? message)) {

                        // Контроль всплесков для пользователя
                        await ControlUserBurst(message.ChatId);

                        // Учитываем глобальный лимит
                        await EnsureGlobalRateLimit();

                        // Отправляем сообщение
                        await SendMessageAsync(message);
                    }
                }
            } finally {
                user.semaphore.Release(); // Освобождаем чат для следующего сообщения
            }
        }

        private static async Task SendMessageAsync(IMessageQueue message) {
            string msg = " ";

            try {
                switch(message) {
                    case TextMessage textMessage:
                        msg = textMessage.Text;

                        if(string.IsNullOrWhiteSpace(textMessage.Text)) break;

                        await BotClient.SendMessage(
                                        chatId: textMessage.ChatId,
                                        text: textMessage.Text,
                                        parseMode: textMessage.ParseMode,
                                        linkPreviewOptions: true,
                                        disableNotification: textMessage.DisableNotification,
                                        replyMarkup: textMessage.ReplyMarkup
                                    );
                        break;

                    case PhotoMessage photoMessage:
                        msg = $"PhotoMessage {photoMessage.ChatId}";

                        //await BotClient.SendPhoto(chatId: photoMessage.ChatId, photo: photoMessage.Photo, replyMarkup: photoMessage.ReplyMarkup, caption: photoMessage.Caption, hasSpoiler: photoMessage.HasSpoiler, disableNotification: photoMessage.DisableNotification, parseMode: photoMessage.ParseMode);
                        await TgSendExtensions.SendPhotoSmart(BotClient, photoMessage.ChatId, photoMessage.Photo, photoMessage.ReplyMarkup, photoMessage.Caption, photoMessage.HasSpoiler, photoMessage.DisableNotification, photoMessage.ParseMode);

                        if(File.Exists(photoMessage.Path)) File.Delete(photoMessage.Path);

                        break;

                }
            } catch(ApiRequestException ex) when(
                                        ex.Message.Contains("bot was blocked by the user") ||
                                        ex.Message.Contains("user is deactivated") ||
                                        ex.Message.Contains("chat not found") ||
                                        ex.Message.Contains("the group chat was deleted") ||
                                        ex.Message.Contains("bot was kicked from the group chat")
                                        ) {

                using(ScheduleDbContext dbContext = new()) {
                    DB.Entity.TelegramUser user = await dbContext.TelegramUsers.FirstAsync(u => u.ChatID == message.ChatId.Identifier);

                    user.IsDeactivated = true;

                    dbContext.SaveChanges();
                }
            } catch(ApiRequestException ex) when(
                                        ex.Message.Contains("message is not modified") ||
                                        ex.Message.Contains("message to delete not found") ||
                                        ex.Message.Contains("VOICE_MESSAGES_FORBIDDEN") ||
                                        ex.Message.Contains("message can't be deleted for everyone")
                                        ) {

            } catch(Exception e) {
                await ErrorReport.Send(msg, e);
            }
        }

        private static async Task ControlUserBurst(ChatId chatId) {
            DateTime currentTime = DateTime.UtcNow;

            (int burstCount, DateTime lastMessageTime) = userBurstControl.GetOrAdd(chatId, _ => (0, DateTime.MinValue));

            if(currentTime - lastMessageTime > burstInterval) {
                // Сбрасываем счетчик всплесков, если интервал превышен
                burstCount = 0;
            }

            if(burstCount >= burstLimit) {
                // Если достигнут лимит всплесков, ждем до следующего разрешенного интервала
                TimeSpan delay = burstInterval - (currentTime - lastMessageTime);
                if(delay > TimeSpan.Zero) {
                    await Task.Delay(delay);
                }

                // Обновляем время последнего сообщения после задержки
                lastMessageTime = DateTime.UtcNow;
                burstCount = 0; // Сбрасываем счетчик всплесков после задержки
            }

            // Обновляем данные всплесков для пользователя
            userBurstControl[chatId] = (burstCount + 1, currentTime);
        }

        private static async Task EnsureGlobalRateLimit() {
            await globalLock.WaitAsync(); // Блокируем глобальный счётчик

            try {
                DateTime currentTime = DateTime.UtcNow;

                // Если прошло больше секунды с последнего сброса, сбрасываем счётчик
                if((currentTime - lastGlobalResetTime).TotalSeconds >= 1) {
                    globalMessageCount = 0;
                    lastGlobalResetTime = currentTime;
                }

                // Если достигли лимита в 30 сообщений в секунду, делаем задержку
                if(globalMessageCount >= globalRateLimit) {
                    TimeSpan delay = globalRateLimitInterval - (currentTime - lastGlobalResetTime);
                    if(delay > TimeSpan.Zero) {
                        await Task.Delay(delay);
                    }

                    globalMessageCount = 0;
                    lastGlobalResetTime = DateTime.UtcNow;
                }

                globalMessageCount++; // Увеличиваем счётчик сообщений

            } finally {
                globalLock.Release(); // Освобождаем глобальную блокировку
            }
        }
    }
}
