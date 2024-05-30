namespace ContentManager_Server
{
    public class ConsoleService : BaseModule
    {
        public ConsoleService() : base("Console")
        {
            Start();
            Logger.Instance.Log("ConsoleService started.", this);
        }

        public void Start()
        {
            Task.Run(() => ListenForCommands());
        }

        private async Task ListenForCommands()
        {
            while (true)
            {
                string? command = Console.ReadLine();
                if (command == null)
                    continue;

                switch (command)
                {
                    case "/help":
                        Logger.Instance.Log("Available commands:", this);
                        Logger.Instance.Log("/help - Show this help message", this);
                        Logger.Instance.Log("/add_image <path> - Add an image from the specified path", this);
                        break;

                    case "/add_image":
                        try
                        {
                            if (Server.ImageService != null)
                            {
                                await Server.ImageService.AddFileAsync();
                            }
                            else
                            {
                                Logger.Instance.Log("ImageService is not initialized.", this);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Log($"Error adding image: {ex.Message}", this);
                        }
                        break;
                    case var cmd when cmd.StartsWith("/get_image"):
                        var parts = cmd.Split(' ');
                        if (parts.Length == 2)
                        {
                            string imageId = parts[1];
                            try
                            {
                                if (Server.ImageService != null)
                                {
                                    string? fullPath = await Server.ImageService.GetFilePathAsync(imageId);
                                    if (fullPath != null)
                                        Logger.Instance.Log($"Full path to image: {fullPath}", this);
                                }
                            }
                            catch(Exception ex)
                            {
                                Logger.Instance.Log($"Error getting image: {ex.Message}", this);
                            }
                        }
                        else
                        {
                            Logger.Instance.Log("Usage: /get_image <image_id>", this);
                        }
                        break;
                    default:
                        Logger.Instance.Log("Unknown command. Type /help for a list of commands.", this);
                        break;
                }
            }
        }
    }
}
