using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using System.Text;
using Newtonsoft.Json;

namespace ContentManager_Server
{
    public static class Server
    {
        private static ConcurrentDictionary<string, ClientHandler> clients = new ConcurrentDictionary<string, ClientHandler>();
        private const string CONFIG_PATH = "server_config.json";
        public static DBController? DatabaseController { get; private set; }
        public static ImageService? ImageService { get; private set; }
        private static ConsoleService? ConsoleService;
        private static TcpListener? listener;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static ManualResetEvent shutdownEvent = new ManualResetEvent(false);
        private static bool isShuttingDown = false;
        private static int _firstChanceExceptionReentrancyLocked;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
            {
                if (Interlocked.CompareExchange(ref _firstChanceExceptionReentrancyLocked, 1, 0) == 0)
                {
                    try
                    {
                        StackTrace currentStackTrace;

                        try
                        {
                            currentStackTrace = new StackTrace(1, true);
                        }
                        catch
                        {
                            currentStackTrace = null;
                        }

                        Logger.Instance.Log(new StringBuilder()
                            .AppendLine($"{DateTime.Now:O} exception thrown: {eventArgs.Exception.Message}")
                            .AppendLine("----- Exception -----")
                            .AppendLine(eventArgs.Exception.ToString().TrimEnd())
                            .AppendLine("----- Full Stack -----")
                            .AppendLine(currentStackTrace?.ToString().TrimEnd())
                            .AppendLine()
                            .ToString());
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _firstChanceExceptionReentrancyLocked, 0);
                    }
                }
            };

            int port = 12361; // стандартный порт

            DatabaseController = new DBController("127.0.0.1", "contentmanagerapp_db", "root", "DeNiskA22565");

            try
            {
                var config = ReadConfig(CONFIG_PATH);
                if (config?.Port > 0 && config.Port <= 65535)
                {
                    port = config.Port;
                }
                else
                {
                    Logger.Instance.Log("Invalid port number in config. Using default port 12361.");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error reading config file: {ex.Message}. Using default port 12361.");
            }

            await DatabaseController.NormalizeTablesAsync();

            ImageService = new ImageService(DatabaseController);
            ImageService.LoadingImage = await DatabaseController.GetLoadingImageFileAsync();

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Logger.Instance.Log($"Server started on port {port}...");

            ConsoleService = new ConsoleService();

            // Добавляем обработчики событий завершения работы приложения
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Предотвращаем закрытие консоли по умолчанию
                CloseServer();   // Закрываем сервер
            };

            await AcceptClientsAsync(cancellationTokenSource.Token);
        }

        private static async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            if (listener == null)
                return;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (listener.Pending())
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        var remoteEndPoint = $"{((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port}";
                        string clientId = GenerateClientId();
                        Logger.Instance.Log($"Client {remoteEndPoint} with ID {clientId} connected.");
                        ClientHandler handler = new ClientHandler(client, clientId, remoteEndPoint);
                        clients.TryAdd(clientId, handler);
                        Thread clientThread = new Thread(new ThreadStart(handler.HandleClient));
                        clientThread.Start();
                    }
                    else
                    {
                        await Task.Delay(100); // Ждем небольшую задержку, чтобы не загружать процессор
                    }
                }
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Исключение ObjectDisposedException ожидается при остановке listener
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error accepting clients: {ex.Message}");
            }
        }

        public static void RemoveClient(string clientId)
        {
            clients.TryRemove(clientId, out _);
        }

        public static void SendMessageToClientById(string clientId, string message)
        {
            clients.TryGetValue(clientId, out ClientHandler? client);
            if (client == null)
                return;
            client.SendMessageToClient(message);
        }

        public static void SendMessageToAllClients(string message)
        {
            foreach (ClientHandler client in clients.Values)
            {
                client.SendMessageToClient(message);
            }
        }

        private static ServerConfig? ReadConfig(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ServerConfig>(json);
        }

        private static string GenerateClientId()
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            char[] stringChars = new char[32];

            for (int i = 0; i < 16; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("D16");

            for (int i = 0; i < 16; i++)
            {
                stringChars[16 + i] = timestamp[i];
            }

            return new string(stringChars);
        }

        public static int GetCountActiveUsers()
        {
            return clients.Count;
        }

        private static void CloseServer()
        {
            if (isShuttingDown) return;
            isShuttingDown = true;

            Logger.Instance.Log("Shutting down server...");
            cancellationTokenSource.Cancel();

            foreach (var clientHandler in clients.Values)
            {
                clientHandler.Disconnect();
            }

            listener?.Stop();
            DatabaseController?.Dispose();
            shutdownEvent.Set();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            CloseServer();
            shutdownEvent.WaitOne();
        }
    }

    class ServerConfig
    {
        public int Port { get; set; }
    }
}
