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
                var freeGames = await EpicGameServices.GetEpicFreeGames();

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
                DebugService.DebugLogException($"Error channel.SendMessage: ", ex.Message);
            }
        }
    }
}
