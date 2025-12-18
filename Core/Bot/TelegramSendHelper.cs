using System.Net;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public static class TgSendExtensions {
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp() {
        var handler = new HttpClientHandler {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        var client = new HttpClient(handler) {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // иногда CDN режут "ботов" без UA
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Bot/1.0)");
        return client;
    }

    public static async Task<Message> SendPhotoSmart(
        this ITelegramBotClient bot,
        ChatId chatId,
        Telegram.Bot.Types.InputFile photo,
        ReplyMarkup? replyMarkup = null,
        string? caption = null,
        bool hasSpoiler = false,
        bool disableNotification = false,
        ParseMode parseMode = default,
        CancellationToken ct = default) {
        // Если это не URL — просто шлём как обычно
        var uri = TryGetHttpUri(photo);
        if(uri is null) {
            return await bot.SendPhoto(
                chatId: chatId,
                photo: photo,
                replyMarkup: replyMarkup,
                caption: caption,
                hasSpoiler: hasSpoiler,
                disableNotification: disableNotification,
                parseMode: parseMode,
                cancellationToken: ct);
        }

        // 1) Быстрая попытка: пусть Telegram скачает сам (если сможет)
        try {
            return await bot.SendPhoto(
                chatId: chatId,
                photo: photo,
                replyMarkup: replyMarkup,
                caption: caption,
                hasSpoiler: hasSpoiler,
                disableNotification: disableNotification,
                parseMode: parseMode,
                cancellationToken: ct);
        } catch(ApiRequestException ex) when(LooksLikeTelegramCantFetchUrl(ex)) {
            // 2) Fallback: качаем сами и отправляем как файл/документ
            return await DownloadAndSend(bot, chatId, uri, replyMarkup, caption, disableNotification, parseMode, ct);
        }
    }

    private static bool LooksLikeTelegramCantFetchUrl(ApiRequestException ex) {
        var msg = (ex.Message ?? "").ToLowerInvariant();
        return msg.Contains("wrong type of the web page content")
            || msg.Contains("failed to get http url content")
            || msg.Contains("wrong file identifier/http url specified");
    }

    private static async Task<Message> DownloadAndSend(
        ITelegramBotClient bot,
        ChatId chatId,
        Uri uri,
        ReplyMarkup? replyMarkup,
        string? caption,
        bool disableNotification,
        ParseMode parseMode,
        CancellationToken ct) {
        using var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var contentType = resp.Content.Headers.ContentType?.MediaType; // image/jpeg etc
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);

        var isImageByHeader = !string.IsNullOrWhiteSpace(contentType) &&
                              contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        var isImageByMagic = LooksLikeImageByMagic(bytes, out var extFromMagic);

        var ext = extFromMagic ?? ExtensionFromContentType(contentType) ?? ".bin";
        var fileName = "file" + ext;

        await using var ms = new MemoryStream(bytes);
        ms.Position = 0;

        if(isImageByHeader || isImageByMagic) {
            return await bot.SendPhoto(
                chatId: chatId,
                photo: Telegram.Bot.Types.InputFile.FromStream(ms, fileName),
                replyMarkup: replyMarkup,
                caption: caption,
                disableNotification: disableNotification,
                parseMode: parseMode,
                cancellationToken: ct);
        }

        // если это не фото — шлём как документzszs
        return await bot.SendDocument(
            chatId: chatId,
            document: Telegram.Bot.Types.InputFile.FromStream(ms, fileName),
            replyMarkup: replyMarkup,
            caption: caption,
            disableNotification: disableNotification,
            parseMode: parseMode,
            cancellationToken: ct);
    }

    private static Uri? TryGetHttpUri(Telegram.Bot.Types.InputFile inputFile) {
        // InputFileUrl / InputFileString и т.п. — достанем URL рефлексией, чтобы не зависеть от точного API
        var t = inputFile.GetType();
        var name = t.Name;

        if(!name.Contains("Url", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("String", StringComparison.OrdinalIgnoreCase))
            return null;

        var p = t.GetProperty("Url") ?? t.GetProperty("Uri") ?? t.GetProperty("Data") ?? t.GetProperty("Value");
        var v = p?.GetValue(inputFile);

        Uri? uri = v switch {
            Uri u => u,
            string s when Uri.TryCreate(s, UriKind.Absolute, out var u) => u,
            _ => null
        };

        if(uri is null) return null;
        if(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        return uri;
    }

    private static string? ExtensionFromContentType(string? mediaType) =>
        (mediaType ?? "").ToLowerInvariant() switch {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            _ => null
        };

    private static bool LooksLikeImageByMagic(byte[] bytes, out string? ext) {
        ext = null;
        if(bytes.Length < 12) return false;

        if(bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) { ext = ".jpg"; return true; } // JPEG
        if(bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) { ext = ".png"; return true; } // PNG
        if(bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) { ext = ".gif"; return true; } // GIF
        if(bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) { ext = ".webp"; return true; } // WEBP

        return false;
    }
}
