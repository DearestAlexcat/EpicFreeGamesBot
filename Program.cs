using Discord;
using Discord.WebSocket;

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
            Debug.Log("-> " + log);
            return Task.CompletedTask;
        }

        static Task Ready()
        {
            Debug.Log("Bot is ready!", ConsoleColor.Green);
            return Task.CompletedTask;
        }

      

        // ---------------------------------------------------------------------------------------------------

        static async Task MessageReceived(SocketMessage arg)
        {
            if (arg is SocketUserMessage message)
            {
                if (message.Author.IsBot)
                {
                    Debug.Log("Message is from a bot, ignoring.", ConsoleColor.Cyan);
                    return;
                }

                if (message.Content != string.Empty)
                {
                    if (message.Content.ToLower() == "!freegames")  // Command to receive game announcements
                    {
                        var freeGames = await EpicGameServices.GetEpicFreeGames();
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

                            if (loadedGames != null)
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
                        Debug.Log("Command !freegames FAILED", ConsoleColor.Red);
                    }
                }
                else
                {
                    Debug.Log("message.Content is empty", ConsoleColor.Red);
                }
            }
        }

        static List<FreeGame> GetUnwathedGames(List<string> loadedGames, List<FreeGame> freeGames)
        {
            if (loadedGames == null || loadedGames.Count == 0)
                return freeGames;
            return freeGames.Where(game => !loadedGames.Contains(game.Title)).ToList();
        }
    }
}