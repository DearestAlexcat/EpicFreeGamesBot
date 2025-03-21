using Discord;
using Discord.WebSocket;

namespace EpicFreeGamesBot
{
    class Program
    {
        private static DiscordSocketClient _client;
        public static IMessageChannel? channel;

        static async Task Main(string[] args)
        {
            // Initialize Discord client
            _client = InitializeDiscordClient();

            // Launch the scheduler
            await Scheduler.StartScheduler();
            
     
            // Bot token - consider storing this in environment variables
            var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? "Token";
               Console.WriteLine($"Token: {token}");
            // Connect to Discord
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Get the channel to send messages to
            ulong channelId = Convert.ToUInt64(Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID") ?? "1111");
            channel = await _client.GetChannelAsync(channelId) as IMessageChannel;
            
         
            Console.WriteLine($"Channel ID: {channelId}");
            // Keep the program running
            await Task.Delay(-1);
        }

        private static DiscordSocketClient InitializeDiscordClient()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            };

            var client = new DiscordSocketClient(config);
            client.Log += Log;
            client.Ready += Ready;
            client.MessageReceived += MessageReceived;

            return client;
        }

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

        static async Task MessageReceived(SocketMessage arg)
        {
            // Only process messages that are user messages
            if (arg is not SocketUserMessage message) return;

            // Ignore bot messages
            if (message.Author.IsBot)
            {
                Debug.Log("Message is from a bot, ignoring.", ConsoleColor.Cyan);
                return;
            }

            // Check if message is empty
            if (string.IsNullOrEmpty(message.Content))
            {
                Debug.Log("message.Content is empty", ConsoleColor.Red);
                return;
            }

            // Process commands
            if (message.Content.ToLower() == "!freegames")
            {
                await HandleFreeGamesCommand(message);
            }
            else if (message.Content.ToLower() == "!cleartracker")
            {
                await HandleClearTrackerCommand(message);
            }
        }

        static async Task HandleFreeGamesCommand(SocketUserMessage message)
        {
            try
            {
                Debug.Log("Fetching free games from Epic Games Store...", ConsoleColor.Yellow);

                var freeGames = await EpicGameServices.GetEpicFreeGames();

                if (freeGames.Count == 0)
                {
                    await message.Channel.SendMessageAsync("No free games available at the moment.");
                    return;
                }

                Debug.Log($"Found {freeGames.Count} free games", ConsoleColor.Green);

                foreach (var game in freeGames)
                {
                    var embed = CreateGameEmbed(game);
                    await message.Channel.SendMessageAsync(embed: embed);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error processing free games command:", ex.Message);
                await message.Channel.SendMessageAsync("An error occurred while fetching free games.");
            }
        }

        static async Task HandleClearTrackerCommand(SocketUserMessage message)
        {
            try
            {
                // Check if user has permission to manage messages
                var guildUser = message.Author as IGuildUser;
                if (guildUser == null || !guildUser.GuildPermissions.ManageMessages)
                {
                    await message.Channel.SendMessageAsync("You don't have permission to use this command.");
                    return;
                }

                var tracker = new PinnedMessageTracker(message.Channel);
                var trackerMessage = await tracker.GetOrCreateTrackerMessage();

                // Reset the tracker message to empty state
                await trackerMessage.ModifyAsync(m => m.Content = "## Epic Games Store - Free Games Tracker\n\nNo games tracked yet.");

                await message.Channel.SendMessageAsync("Game tracker has been cleared.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Error clearing tracker:", ex.Message);
                await message.Channel.SendMessageAsync("An error occurred while clearing the tracker.");
            }
        }

        static Embed CreateGameEmbed(FreeGame game)
        {
            return new EmbedBuilder
            {
                Title = game.Title,
                Description = game.Description,
                ImageUrl = game.ImageUrl,
                Url = game.Url,
                Color = Color.Blue,
                Footer = new EmbedFooterBuilder
                {
                    Text = "Epic Games Store Free Game"
                },
                Timestamp = DateTimeOffset.Now
            }.Build();
        }
    }
}