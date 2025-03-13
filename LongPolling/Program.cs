using System.Globalization;

using Core.Bot;

using Telegram.Bot;
using Telegram.Bot.Polling;

namespace LongPolling {
    internal class Program {
        static void Main() {
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
        }
    }
}