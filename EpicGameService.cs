using Newtonsoft.Json.Linq;

namespace EpicFreeGamesBot
{
    static class EpicGameServices
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string EpicGamesApiUrl = "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions";

        static public async Task<List<FreeGame>> GetEpicFreeGames()
        {
            try
            {
                var response = await httpClient.GetStringAsync(EpicGamesApiUrl);
                var json = JObject.Parse(response);

                // Safely navigate the JSON structure
                var games = json["data"]?["Catalog"]?["searchStore"]?["elements"] as JArray;

                if (games == null)
                {
                    Debug.Log("No games found in the API response.", ConsoleColor.Red);
                    return new List<FreeGame>();
                }

                return ExtractFreeGames(games);
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError("HTTP request failed:", ex.Message);
                return new List<FreeGame>();
            }
            catch (Exception ex)
            {
                Debug.LogError("Error getting free games:", ex.Message);
                return new List<FreeGame>();
            }
        }

        private static List<FreeGame> ExtractFreeGames(JArray games)
        {
            var freeGames = new List<FreeGame>();

            foreach (var game in games)
            {
                try
                {
                    // Check if the game has promotions
                    var promotions = game["promotions"] as JObject;
                    if (promotions == null) continue;

                    // Check if promotionalOffers exists and is an array
                    var promotionalOffers = promotions["promotionalOffers"] as JArray;
                    if (promotionalOffers == null || !promotionalOffers.HasValues) continue;

                    // Iterate through promotional offers
                    foreach (var offer in promotionalOffers)
                    {
                        var promoOffers = offer["promotionalOffers"] as JArray;
                        if (promoOffers == null || !promoOffers.HasValues) continue;

                        foreach (var promo in promoOffers)
                        {
                            // Check if the discount percentage is 0 (free game)
                            var discountSetting = promo["discountSetting"] as JObject;
                            if (discountSetting == null) continue;

                            var discountPercentage = discountSetting["discountPercentage"]?.ToString();
                            if (discountPercentage == "0")
                            {
                                // Add the free game to the list
                                freeGames.Add(new FreeGame
                                {
                                    Title = game["title"]?.ToString() ?? "No Title",
                                    Description = game["description"]?.ToString() ?? "No Description",
                                    ImageUrl = GetBestImageUrl(game),
                                    Url = ConstructGameUrl(game)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing game: ", ex.Message);
                }
            }

            return freeGames;
        }

        private static string GetBestImageUrl(JToken game)
        {
            var keyImages = game["keyImages"] as JArray;

            if (keyImages == null || !keyImages.HasValues)
                return "No Image";

            // Try to find the best image - prefer "OfferImageWide" type
            foreach (var image in keyImages)
            {
                string type = image["type"]?.ToString();
                if (type == "OfferImageWide" || type == "DieselStoreFrontWide")
                {
                    return image["url"]?.ToString() ?? "No Image";
                }
            }

            // Fall back to the first image if no preferred type is found
            return keyImages[0]["url"]?.ToString() ?? "No Image";
        }

        private static string ConstructGameUrl(JToken game)
        {
            // Try to get the productSlug directly (most reliable for full games)
            var productSlug = game["productSlug"]?.ToString();
            if (!string.IsNullOrEmpty(productSlug))
            {
                return $"https://www.epicgames.com/store/en-US/p/{productSlug}";
            }

            // Check offerMappings first (used for DLCs or special offers)
            var offerMappings = game["offerMappings"] as JArray;
            if (offerMappings != null && offerMappings.Count > 0)
            {
                var firstOffer = offerMappings[0];
                var pageSlug = firstOffer["pageSlug"]?.ToString();
                if (!string.IsNullOrEmpty(pageSlug))
                {
                    return $"https://www.epicgames.com/store/en-US/p/{pageSlug}";
                }
            }

            // Fallback to catalogNs.mappings (used for full games)
            var catalogNs = game["catalogNs"] as JObject;
            if (catalogNs != null)
            {
                var mappings = catalogNs["mappings"] as JArray;
                if (mappings != null && mappings.Count > 0)
                {
                    var firstMapping = mappings[0];
                    var pageSlug = firstMapping["pageSlug"]?.ToString();
                    if (!string.IsNullOrEmpty(pageSlug))
                    {
                        return $"https://www.epicgames.com/store/en-US/p/{pageSlug}";
                    }
                }
            }

            // If no valid URL found, return default store URL
            return "https://www.epicgames.com/store/en-US/";
        }
    }
}