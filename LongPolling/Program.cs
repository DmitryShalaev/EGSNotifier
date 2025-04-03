using System.Globalization;

using Core.Bot;
using Core.DB;
using Core.Parser;

using Telegram.Bot;
using Telegram.Bot.Polling;

namespace LongPolling {
    internal class Program {
        static async Task Main() {
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");


            TelegramBot.botClient.ReceiveAsync(
                (botClient, update, cancellationToken) => TelegramBot.UpdateAsync(update),
                (botClient, update, cancellationToken) => Task.CompletedTask,
                new ReceiverOptions {
                    AllowedUpdates = { },
#if DEBUG
                    DropPendingUpdates = true
#else
                    DropPendingUpdates = false
#endif
                },
                new CancellationTokenSource().Token
            ).Wait();


            using(ScheduleDbContext dbContext = new()) {

                await EGSParser.UpdatingEGS(dbContext);
            }
        }
    }
}