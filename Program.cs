using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;

namespace EpicFreeGamesBot
{
    class Program
    {
        public static IMessageChannel? channel;
        public static string fileName = "watched_games.json";

        static async Task Main(string[] args)
        {
            // Launch the scheduler
            await Scheduler.StartScheduler();   

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent 
            };

            var token = "Token"; // Replace with your TOKEN

            var client = new DiscordSocketClient(config);
            client.Log += Log;
            client.Ready += Ready;
            client.MessageReceived += MessageReceived;

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            channel = await client.GetChannelAsync(1111) as IMessageChannel; // Replace with your CHANNEL_ID

            // The bot will work until it is completed.
            await Task.Delay(-1);
        }

        // ---------------------------------------------------------------------------------------------------

        static Task Log(LogMessage log)
        {
            DebugLog("-> " + log);
            return Task.CompletedTask;
        }

        static Task Ready()
        {
            DebugLog("Bot is ready!", ConsoleColor.Green);
            return Task.CompletedTask;
        }

        static public void DebugLog(string msg, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static public void DebugLogException(params string[] msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg[0]);
            Console.ForegroundColor = ConsoleColor.Gray;
            foreach (var message in msg)
                Console.WriteLine(message);
        }

        // ---------------------------------------------------------------------------------------------------

        static async Task MessageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage message)
            {
                if (message.Author.IsBot)
                {
                    DebugLog("Message is from a bot, ignoring.", ConsoleColor.Cyan);
                    return;
                }

                if (message.Content != string.Empty)
                {
                    if (message.Content.ToLower() == "!freegames")  // Command to receive game announcements
                    {
                        var freeGames = await GetEpicFreeGames();
                        var loadedGames = await GoogleCloudStorageHelper.LoadWatchedGamesAsync(fileName);  // Loading a list of games from Google Cloud Storage
                        
                        freeGames = GetUnwathedGames(loadedGames, freeGames);

                        foreach (var game in freeGames)
                        {
                            var embed = new EmbedBuilder
                            {
                                Title = game.Title,
                                Description = game.Description,
                                ImageUrl = game.ImageUrl,
                                Url = game.Url,
                                Color = Color.Blue
                            }.Build();

                            if(loadedGames != null)
                            {
                                // Save your game list to Google Cloud Storage
                                var combinedTitles = loadedGames.Concat(freeGames.Select(game => game.Title)).ToList();
                                await GoogleCloudStorageHelper.SaveWatchedGamesAsync(fileName, combinedTitles);
                            }

                            await message.Channel.SendMessageAsync(embed: embed);
                        }
                    }
                    else
                    {
                        DebugLog("Command !freegames FAILED", ConsoleColor.Red);
                    }
                }
                else
                {
                    DebugLog("message.Content is empty", ConsoleColor.Red);
                }
            }
        }

        static List<FreeGame> GetUnwathedGames(List<string> loadedGames, List<FreeGame> freeGames)
        {
            if (loadedGames == null || loadedGames.Count == 0)
                return freeGames;
            return freeGames.Where(game => !loadedGames.Contains(game.Title)).ToList();
        }

        static public async Task<List<FreeGame>> GetEpicFreeGames()
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync("https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions");
            var json = JObject.Parse(response);

            // Safely navigate the JSON structure
            var games = json["data"]?["Catalog"]?["searchStore"]?["elements"] as JArray;

            if (games == null)
            {
                DebugLog("No games found in the API response.", ConsoleColor.Red);
                return new List<FreeGame>();
            }

            var freeGames = new List<FreeGame>();

            foreach (var game in games)
            {
                try
                {
                    // Check if the game has promotions
                    var promotions = game["promotions"] as JObject;
                    if (promotions == null)
                    {
                        continue; // Skip this game if there are no promotions
                    }

                    // Check if promotionalOffers exists and is an array
                    var promotionalOffers = promotions["promotionalOffers"] as JArray;
                    if (promotionalOffers == null || !promotionalOffers.HasValues)
                    {
                        continue; // Skip this game if there are no promotional offers
                    }

                    // Iterate through promotional offers
                    foreach (var offer in promotionalOffers)
                    {
                        var promoOffers = offer["promotionalOffers"] as JArray;
                        if (promoOffers == null || !promoOffers.HasValues)
                        {
                            continue; // Skip this offer if it has no valid promotions
                        }

                        foreach (var promo in promoOffers)
                        {
                            // Check if the discount percentage is 0 (free game)
                            var discountSetting = promo["discountSetting"] as JObject;
                            if (discountSetting == null)
                            {
                                continue; // Skip if discountSetting is missing
                            }

                            var discountPercentage = discountSetting["discountPercentage"]?.ToString();
                            if (discountPercentage == "0")
                            {
                                // Add the free game to the list
                                freeGames.Add(new FreeGame
                                {
                                    Title = game["title"]?.ToString() ?? "No Title",
                                    Description = game["description"]?.ToString() ?? "No Description",
                                    ImageUrl = game["keyImages"]?[0]?["url"]?.ToString() ?? "No Image",
                                    Url = ConstructGameUrl(game)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogException($"Error processing game: ", ex.Message);
                }
            }

            return freeGames;
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