using System.Globalization;
using System.Net;

using Core.Bot;
using Core.DB;
using Core.DB.Entity;

using Newtonsoft.Json.Linq;

namespace Core.Parser {
    public static class EGSParser {
        private static readonly HttpClientHandler clientHandler;

        static EGSParser() {
            clientHandler = new() {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None,
            };
        }

        public static async Task<List<EGS>?> GetEGS() {
            try {
                using(var client = new HttpClient(clientHandler, false)) {
                    #region RequestHeaders
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
                    client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36 Edg/112.0.1722.34");
                    client.DefaultRequestHeaders.Add("Origin", "https://store.epicgames.com");
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");

                    client.Timeout = TimeSpan.FromSeconds(10);

                    #endregion

                    using(HttpResponseMessage response = await client.GetAsync($"https://store-site-backend-static-ipv4.ak.epicgames.com/freeGamesPromotions?locale=US&country=US&allowCountries=US")) {
                        if(response.IsSuccessStatusCode) {
                            var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());
                            if(jObject.Count == 0) throw new Exception();

                            List<JObject> elements = jObject["data"]?["Catalog"]?["searchStore"]?["elements"]?.ToObject<List<JObject>>() ?? throw new Exception();

                            var egs = elements
                                .SelectMany(element => {
                                    // Извлечение всех промо-контейнеров из текущего элемента
                                    JToken? promotions = element["promotions"];
                                    if(promotions == null || promotions.Type != JTokenType.Object)
                                        return [];

                                    JToken? promoContainers = promotions["promotionalOffers"];
                                    if(promoContainers == null || promoContainers.Type != JTokenType.Array)
                                        return [];

                                    // Извлечение всех промо-акций внутри каждого контейнера
                                    var promotionalOffers = promoContainers
                                        .SelectMany(promoContainer => promoContainer["promotionalOffers"]?.ToObject<List<JObject>>() ?? Enumerable.Empty<JObject>())
                                        .Where(promo => promo["discountSetting"]?["discountPercentage"]?.Value<int>() == 0)
                                        .ToList();

                                    // Если промо-акций с нулевой скидкой нет, возвращаем пустой список
                                    if(promotionalOffers == null || promotionalOffers.Count == 0)
                                        return [];

                                    // Преобразуем каждую промо-акцию в объект EGS
                                    return promotionalOffers.Select(promo => {
                                        string title = element.Value<string>("title") ?? string.Empty;
                                        string description = element.Value<string>("description") ?? string.Empty;
                                        string thumbnail = element["keyImages"]?
                                            .FirstOrDefault(i => i.Value<string>("type") == "Thumbnail")?
                                            .Value<string>("url") ?? "https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Epic_games_store_logo.svg/800px-Epic_games_store_logo.svg.png";
                                        string pageSlug = element["catalogNs"]?["mappings"]?
                                            .FirstOrDefault(i => i.Value<string>("pageType") == "productHome")?
                                            .Value<string>("pageSlug") ?? string.Empty;
                                        string originalPrice = element["price"]?["totalPrice"]?["fmtPrice"]?.Value<string>("originalPrice") ?? "₽";

                                        // Обработка дат
                                        DateTime.TryParseExact(
                                            promo.Value<string>("startDate"),
                                            "MM/dd/yyyy HH:mm:ss",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeUniversal,
                                            out DateTime startDate);
                                        DateTime.TryParseExact(
                                            promo.Value<string>("endDate"),
                                            "MM/dd/yyyy HH:mm:ss",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeUniversal,
                                            out DateTime endDate);

                                        // Возврат объекта EGS
                                        return new EGS(
                                            title: title,
                                            description: description,
                                            thumbnail: thumbnail,
                                            page: pageSlug,
                                            originalPrice: originalPrice,
                                            startDate: startDate.ToUniversalTime(),
                                            endDate: endDate.ToUniversalTime()
                                        );
                                    });
                                })
                                .ToList();

                            return egs;
                        }
                    }
                }
            } catch(Exception) {
                return null;
            }

            return null;
        }

        public static async Task<bool> UpdatingEGS(ScheduleDbContext dbContext) {

            List<EGS>? EGSs = await GetEGS();

            if(EGSs is not null) {
                var _list = dbContext.EGS.ToList();

                IEnumerable<EGS> except = EGSs.Except(_list);
                if(except.Any()) {
                    await dbContext.EGS.AddRangeAsync(except);

                    await dbContext.SaveChangesAsync();

                    Notifications.UpdatedEGS([.. except]);
                }

                return true;
            }

            return false;
        }
    }
}
