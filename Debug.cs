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
            Console.Write(msg[0]);
            Console.ForegroundColor = ConsoleColor.Gray;

            if(msg.Length == 1) Console.WriteLine();

            for (int i = 1; i < msg.Length; i++)
                Console.WriteLine(msg[i]);
        }
    }
}
