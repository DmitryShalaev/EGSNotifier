using System.Globalization;

using Core.Bot;

using Microsoft.AspNetCore.Localization;

using Telegram.Bot.Types;

namespace WebHook {

    public class TelegramUpdateBackgroundService {
        public async void ProcessUpdateAsync(Update update) => await TelegramBot.UpdateAsync(update);
    }

    public class Program {
        public static async Task Main(string[] args) {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            builder.Services.ConfigureTelegramBotMvc();
            builder.Services.AddSingleton<TelegramUpdateBackgroundService>();

            builder.Services.AddControllers();

            WebApplication app = builder.Build();

            CultureInfo cultureInfo = new("en-US") { DateTimeFormat = { FirstDayOfWeek = DayOfWeek.Sunday }, NumberFormat = { NumberDecimalSeparator = ".", CurrencyDecimalSeparator = "." } };

            app.UseRequestLocalization(new RequestLocalizationOptions {
                DefaultRequestCulture = new RequestCulture(cultureInfo),
                SupportedCultures = [cultureInfo],
                SupportedUICultures = [cultureInfo]
            });

            app.UseAuthorization();
            app.MapControllers();

            app.RunAsync();

        using var scope = app.Services.CreateScope();
        var updateService = scope.ServiceProvider.GetRequiredService<TelegramUpdateBackgroundService>();
        
        var fakeUpdate = new Update { /* ... */ };

        await updateService.ProcessUpdateAsync(fakeUpdate);
        await app.WaitForShutdownAsync();
        }
    }
}