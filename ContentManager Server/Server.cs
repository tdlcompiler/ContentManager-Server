using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using ContentManager_Server.DatabaseEntityCore;
using ContentManager_Server.FileServices;

namespace ContentManager_Server
{
    public static class Server
    {
        private static ConcurrentDictionary<string, ClientHandler> clients = new ConcurrentDictionary<string, ClientHandler>();
        private const string CONFIG_PATH = "server_config.json";
        public static DBController? DatabaseController { get; private set; }
        public static ImageService? ImageService { get; private set; }
        public static PrefabService? PrefabService { get; private set; }
        public static ConsoleService? ConsoleService { get; private set; }
        private static TcpListener? listener;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static ManualResetEvent shutdownEvent = new ManualResetEvent(false);
        private static bool isShuttingDown = false;
        private static int _firstChanceExceptionReentrancyLocked;

        static async Task Main(string[] args)
        {
            int port = 12361;

            DatabaseController = new DBController("127.0.0.1", "contentmanagerapp_db", "root", "DeNiskA22565");
            await DatabaseController.NormalizeTablesAsync();

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

            ImageService = new ImageService(DatabaseController);
            ImageService.LoadingImage = await DatabaseController.GetLoadingImageFileAsync();

            PrefabService = new PrefabService(DatabaseController);

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Logger.Instance.Log($"Server started on port {port}...");

            ConsoleService = new ConsoleService();
s
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                CloseServer();
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
                        if (client.Client.RemoteEndPoint is not IPEndPoint endPoint)
                            return;
                        var remoteEndPoint = $"{endPoint.Address}:{endPoint.Port}";
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
                // Исключение ObjectDisposedException ожидается при остановке listener, поэтому не трогаем, но ловим,
                // чтобы не засорять консоль
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

        public static void SendMessageToAllClients(params string[] args)
        {
            foreach (ClientHandler client in clients.Values)
            {
                client.SendMessageToClient(args);
            }
        }

        public static void SendUpdateUserToOwners(User user)
        {
            SendMessageToAuthClients(UserType.OWNER, "updateuser", JsonUtils.UsersToJsonByOwnerRequest(new List<User> { user }));
        }

        public static void SendUpdateAuthorToUsers(Author author)
        {
            string command = "updateauthor";
            string json = JsonUtils.AuthorsToJsonByUserRequest(new List<Author> { author });
            SendMessageToAuthClients(UserType.ADMINISTRATOR, command, json);
            SendMessageToAuthClients(UserType.EDITOR, command, json);
            SendMessageToAuthClients(UserType.RO_USER, command, json);
        }

        public static void SendRemoveUserToOwners(int userId)
        {
            SendMessageToAuthClients(UserType.OWNER, "removeuser", userId.ToString());
        }

        public static void SendRemoveAuthorToUsers(int authorId)
        {
            string command = "removeauthor";
            string strAuthorId = authorId.ToString();
            SendMessageToAuthClients(UserType.OWNER, command, strAuthorId);
            SendMessageToAuthClients(UserType.EDITOR, command, strAuthorId);
            SendMessageToAuthClients(UserType.RO_USER, command, strAuthorId);
        }

        public static AuthClientHandler? GetAuthClientHandlerByUserId(int userId)
        {
            AuthClientHandler? authClientHandler = null;
            foreach (ClientHandler client in clients.Values)
            {
                if (client?.CurrentUser?.Id == userId)
                {
                    authClientHandler = client.GetAuthClientHandler() as AuthClientHandler;
                    break;
                }
            }
            return authClientHandler;
        }

        public static void SendMessageToAuthClients(UserType role, params string[] message)
        {
            foreach (ClientHandler client in clients.Values)
            {
                if (client?.CurrentUser?.RoleId == (int)role)
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
        public int Port { get; set; } = 12361;
    }
}