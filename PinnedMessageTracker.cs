using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace EpicFreeGamesBot
{
    public class PinnedMessageTracker
    {
        private readonly IMessageChannel _channel;
        private const string PIN_HEADER = "## Epic Games Store - Free Games Tracker\n\n";
        private const int MAX_GAME_AGE_DAYS = 14; // Remove games after 2 weeks

        public PinnedMessageTracker(IMessageChannel channel)
        {
            _channel = channel;
        }

        public async Task<IUserMessage> GetOrCreateTrackerMessage()
        {
            try
            {
                // Get pinned messages in the channel
                var pinnedMessages = await _channel.GetPinnedMessagesAsync();

                // Look for our tracker message (identified by header)
                var trackerMessage = pinnedMessages
                    .OfType<IUserMessage>()
                    .FirstOrDefault(m => m.Content.StartsWith(PIN_HEADER));

                if (trackerMessage != null)
                {
                    Debug.Log("Found existing tracker pinned message", ConsoleColor.Green);
                    return trackerMessage;
                }

                // Create new tracker message if none exists
                Debug.Log("Creating new tracker pinned message", ConsoleColor.Yellow);
                var message = await _channel.SendMessageAsync(PIN_HEADER + "No games tracked yet.");
                await message.PinAsync();
                return message;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error accessing pinned messages:", ex.Message);
                throw;
            }
        }

        public async Task<List<TrackedGame>> GetTrackedGames()
        {
            var trackerMessage = await GetOrCreateTrackerMessage();
            var content = trackerMessage.Content;

            // If we only have the header or there's no content, return empty list
            if (content == PIN_HEADER || content == PIN_HEADER + "No games tracked yet.")
            {
                return new List<TrackedGame>();
            }

            var trackedGames = new List<TrackedGame>();

            // Parse games from the message - format: "- [Game Title](URL) - Added on YYYY-MM-DD"
            var pattern = @"- \[(.*?)\]\((.*?)\) - Added on (\d{4}-\d{2}-\d{2})";
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    trackedGames.Add(new TrackedGame
                    {
                        Title = match.Groups[1].Value,
                        Url = match.Groups[2].Value,
                        AddedDate = DateTime.Parse(match.Groups[3].Value)
                    });
                }
            }

            return trackedGames;
        }

        public async Task UpdateTrackerMessage(List<FreeGame> newGames)
        {
            var trackerMessage = await GetOrCreateTrackerMessage();
            var trackedGames = await GetTrackedGames();
            var today = DateTime.Now.Date;

            // Add new games to the tracked games list
            foreach (var game in newGames)
            {
                // Check if game is already tracked
                if (!trackedGames.Any(g => g.Title == game.Title))
                {
                    trackedGames.Add(new TrackedGame
                    {
                        Title = game.Title,
                        Url = game.Url,
                        AddedDate = today
                    });
                }
            }

            // Remove games older than MAX_GAME_AGE_DAYS
            trackedGames = trackedGames
                .Where(g => (today - g.AddedDate).TotalDays < MAX_GAME_AGE_DAYS)
                .ToList();

            // Build new message content
            var contentBuilder = new System.Text.StringBuilder();
            contentBuilder.Append(PIN_HEADER);

            if (trackedGames.Count == 0)
            {
                contentBuilder.Append("No games tracked yet.");
            }
            else
            {
                foreach (var game in trackedGames)
                {
                    contentBuilder.AppendLine($"- [{game.Title}]({game.Url}) - Added on {game.AddedDate:yyyy-MM-dd}");
                }
            }

            // Update the pinned message
            await trackerMessage.ModifyAsync(m => m.Content = contentBuilder.ToString());
            Debug.Log("Updated tracker message with new games", ConsoleColor.Green);
        }

        public async Task<List<FreeGame>> FilterNewGames(List<FreeGame> allGames)
        {
            var trackedGames = await GetTrackedGames();

            // Return only games that are not already in the tracked list
            return allGames
                .Where(game => !trackedGames.Any(tracked => tracked.Title == game.Title))
                .ToList();
        }
    }

    public class TrackedGame
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public DateTime AddedDate { get; set; }
    }
}