using Discord;
using Quartz.Impl;
using Quartz;

namespace EpicFreeGamesBot
{
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
                .StartNow()
                .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(20, 48))
                .Build();

            await scheduler.ScheduleJob(job, trigger);
            Debug.Log("Scheduler started. Job will run daily at 12:00.", ConsoleColor.Green);
        }
    }

    public class ScheduledTask : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                Debug.Log("Scheduled task running: Fetching free games...", ConsoleColor.Yellow);

                if (Program.channel == null)
                {
                    Debug.LogError("Channel is null, cannot proceed with scheduled task");
                    return;
                }

                // Initialize tracker for pinned messages
                var tracker = new PinnedMessageTracker(Program.channel);

                // Get all current free games
                var allFreeGames = await EpicGameServices.GetEpicFreeGames();

                if (allFreeGames.Count == 0)
                {
                    Debug.Log("No free games found during scheduled check.", ConsoleColor.Yellow);
                    return;
                }

                // Filter out games that have already been announced
                var newFreeGames = await tracker.FilterNewGames(allFreeGames);

                if (newFreeGames.Count == 0)
                {
                    Debug.Log("No new free games to announce.", ConsoleColor.Yellow);
                    return;
                }

                Debug.Log($"Found {newFreeGames.Count} new free games to announce", ConsoleColor.Green);

                // Send messages for new games
                foreach (var game in newFreeGames)
                {
                    var embed = CreateGameEmbed(game);
                    await Program.channel.SendMessageAsync(embed: embed);
                    // Add a small delay to avoid rate limiting
                    await Task.Delay(1000);
                }

                // Update the tracked games in pinned message
                await tracker.UpdateTrackerMessage(allFreeGames);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in scheduled task:", ex.Message, ex.StackTrace);
            }
        }

        private Embed CreateGameEmbed(FreeGame game)
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