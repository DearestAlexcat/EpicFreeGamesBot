namespace EpicFreeGamesBot
{
    static class Debug
    {
        static public void Log(string msg, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        static public void LogError(params string[] msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg[0]);
            Console.ForegroundColor = ConsoleColor.Gray;
            
            foreach (var message in msg)
                Console.WriteLine(message);
        }
    }
}
