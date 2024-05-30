namespace ContentManager_Server
{
    public sealed class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        private readonly object _lock = new object();

        private Logger() { }

        public static Logger Instance => _instance.Value;

        public void Log(string message, BaseModule? callingModule = null)
        {
            if (callingModule == null)
            {
                callingModule = new BaseModule("MainCore");
            }
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(timestamp);
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(callingModule.ModuleName);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("]: ");
                Console.ResetColor();
                Console.WriteLine(message);
            }
        }
    }
}
