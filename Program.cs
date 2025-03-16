using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.AccessControl;

public static class Scheduler
{
   static public async Task StartScheduler()
    {
        var schedulerFactory = new StdSchedulerFactory();
        var scheduler = await schedulerFactory.GetScheduler();

        await scheduler.Start();

        // Create a task and trigger for scheduling execution every day at a specific time.
        var job = JobBuilder.Create<ScheduledTask>()
            .WithIdentity("freeGamesJob", "group1")
            .Build();

        // Trigger for execution every day at 12:00.
        var trigger = TriggerBuilder.Create()
            .WithIdentity("dailyTrigger", "group1")
            //.WithIdentity("thursdayTrigger", "group1")
            .StartNow()
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(12, 00))
            .Build();

        // Планируем выполнение задачи с триггером
        await scheduler.ScheduleJob(job, trigger);
    }
}

public class ScheduledTask : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var freeGames = await Program.GetEpicFreeGames();

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

                await Program.channel.SendMessageAsync(embed: embed);
            }
        }
        catch (Exception ex)
        {
            Program.DebugLogException($"Error channel.SendMessage: ", ex.Message);
        }
    }
}

public class FreeGame
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    public string Url { get; set; }
}

/*
public class FirestoreSaver
{
    private static FirestoreDb _db;
    static FirestoreSaver()
    {
        // Establishing a connection to Firestore
        _db = FirestoreDb.Create("your-project-id");  // Replace with your project ID
    }

    public static async Task SaveWatchedGamesAsync(string collectionName, string documentId, List<string> watchedGames)
    {
        var docRef = _db.Collection(collectionName).Document(documentId);
        await docRef.SetAsync(new { WatchedGames = watchedGames });
        Program.DebugLog("Data saved to Firestore", ConsoleColor.Green);
    }

    public static async Task<List<string>> GetWatchedGamesAsync(string collectionName, string documentId)
    {
        try
        {
            var docRef = _db.Collection(collectionName).Document(documentId);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var watchedGames = snapshot.GetValue<List<string>>("WatchedGames");
                return watchedGames;
            }
            else
            {
                Program.DebugLog("Document not found", ConsoleColor.Red);
                return new List<string>();
            }
        }
        catch (Exception ex)
        {
            Program.DebugLogException("Error loading data from Firestore: ", ex.Message);
            return new List<string>();
        }
    }
}
*/

public class GoogleCloudStorageHelper
{
    private static readonly string bucketName = "your-bucket-name"; // Provide a name bucket
    private static StorageClient storageClient = StorageClient.Create();

    public static async Task SaveWatchedGamesAsync(string fileName, List<FreeGame> watchedGames)
    {
        // Convert a list of games to a JSON string. We save only Title
        string jsonData = JsonConvert.SerializeObject(watchedGames.Select(game => game.Title).ToList());

        // Create a stream to write data to a file
        using (var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonData)))
        {
            // Uploading a file to Cloud Storage
            await storageClient.UploadObjectAsync(bucketName, fileName, "application/json", memoryStream);
            Program.DebugLog("File uploaded to Google Cloud Storage.", ConsoleColor.Green);
        }
    }

    public static async Task<List<string>> LoadWatchedGamesAsync(string fileName)
    {
        try
        {
            // Getting an object from storage
            var obj = await storageClient.GetObjectAsync(bucketName, fileName);

            using (var memoryStream = new MemoryStream())
            {
                // Loading an object into a stream
                await storageClient.DownloadObjectAsync(obj, memoryStream);

                // Convert the stream back to a JSON string
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream))
                {
                    string jsonData = await reader.ReadToEndAsync();
                    var watchedGames = JsonConvert.DeserializeObject<List<string>>(jsonData);

                    Program.DebugLog("File loaded from Google Cloud Storage.", ConsoleColor.Green);

                    if(watchedGames == null)
                        return new List<string>();

                    return watchedGames;
                }
            }
        }
        catch (Exception ex)
        {
            Program.DebugLogException($"Error loading file from Google Cloud Storage: ", ex.Message);
            return new List<string>();
        }
    }
}

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
                    //var unwatchedFreeGames = await CheckViewedGames(freeGames);

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

                        //var combinedList = freeGames.Concat(unwatchedFreeGames).ToList();

                        // Save your game list to Google Cloud Storage
                       //await GoogleCloudStorageHelper.SaveWatchedGamesAsync(fileName, combinedList);

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

    static async Task<List<FreeGame>> CheckViewedGames(List<FreeGame> freeGames)
    {
        // Loading a list of games from Google Cloud Storage
        var loadedGames = await GoogleCloudStorageHelper.LoadWatchedGamesAsync(fileName);
        if (loadedGames == null || loadedGames.Count == 0)
            return freeGames;

        List<FreeGame> unviewedGames = new List<FreeGame>();
        unviewedGames = freeGames.Where(game => !loadedGames.Contains(game.Title)).ToList();
        return unviewedGames;
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