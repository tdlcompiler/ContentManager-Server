using ContentManager_Server.DatabaseEntityCore;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ContentManager_Server
{
    public class ClientHandler : BaseModule, IClientMessageObserver
    {
        private readonly List<IClientMessageObserver> observers = new List<IClientMessageObserver>();
        private TcpClient client;
        private NetworkStream stream;
        private TextWriter? writer;
        private Aes aes;
        private string clientId;
        private string remoteEndPoint;
        public User? CurrentUser { get; private set; }

        public ClientHandler(TcpClient client, string clientId, string remoteEndPoint) : base("ClientHandler")
        {
            this.client = client;
            this.clientId = clientId;
            this.remoteEndPoint = remoteEndPoint;
            stream = client.GetStream();
            aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes("KiJvHecdyYCeM2llswHEmQfICiUtdlxk");
            aes.IV = Encoding.UTF8.GetBytes("U9htdlFa3As7n9nD");
        }

        public IClientMessageObserver? GetAuthClientHandler()
        {
            if (observers.Count > 0)
                return observers[0];
            return null;
        } 

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ResetCurrentUser()
        {
            CurrentUser = null;
        }

        public void HandleClient()
        {
            Logger.Instance.Log($"DateTime of {clientId} connection: {getDateTimeClientConnection():dd.MM.yyyy HH:mm:ss}", this);
            try
            {
                using (stream)
                {
                    using StreamReader reader = new StreamReader(stream, Encoding.UTF8);

                    writer = TextWriter.Synchronized(new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true });
                    string? loadingImageBase64 = Server.ImageService?.GetFileInStringFormat(Server.ImageService.LoadingImage);

                    //if (!string.IsNullOrEmpty(loadingImageBase64))
                    //    SendMessageToClient("setloadingimage", new List<string> { loadingImageBase64 });

                    StringBuilder clientMessageBuilder = new StringBuilder();
                    string clientMessageChunk;
                    while ((clientMessageChunk = reader.ReadLine()) != null)
                    {
                        if (clientMessageChunk == "~end~")
                        {
                            string encryptedMessage = clientMessageBuilder.ToString();

                            //Logger.Instance.Log($"Complete encrypted message: {encryptedMessage}", this);

                            try
                            {
                                string decryptedMessage = DecryptMessage(encryptedMessage);
                                HandleMessageFromClient(decryptedMessage);
                            }
                            catch (FormatException ex)
                            {
                                Logger.Instance.Log($"Decryption error: {ex.Message}. Encrypted message: {encryptedMessage}", this);
                            }

                            clientMessageBuilder.Clear();
                        }
                        else if (clientMessageChunk.StartsWith("~chunk~"))
                        {
                            string chunkData = clientMessageChunk.Remove(0, 7); // Убираем префикс ~chunk~

                            //Logger.Instance.Log($"Received chunk: {chunkData}", this);

                            clientMessageBuilder.Append(chunkData);
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error with client {clientId}: {ex.Message}", this);
            }
            finally
            {
                Server.RemoveClient(clientId);
                client.Close();
                HandleClientDisconnect();
            }
        }

        public void Disconnect()
        {
            foreach (IClientMessageObserver clientMessageObserver in observers)
            {
                clientMessageObserver.Dispose();
            }
            observers.Clear();
            Server.RemoveClient(clientId);
            client.Close();
            HandleClientDisconnect();
        }

        public void SendMessageToClient(string command, List<string> args)
        {
            lock (lockObject)
            {
                if (writer == null)
                {
                    Logger.Instance.Log("StreamWriter is not initialized.", this);
                    return;
                }

                StringBuilder message = new StringBuilder(command);
                foreach (string arg in args)
                {
                    message.Append($"~sp~{arg}");
                }

                string encryptedMessage = EncryptMessage(message.ToString());
                int chunkSize = 32768; // Размер чанка, например, 2048 символов
                try
                {
                    // Отправляем сообщение чанками
                    for (int i = 0; i < encryptedMessage.Length; i += chunkSize)
                    {
                        string chunk = encryptedMessage.Substring(i, Math.Min(chunkSize, encryptedMessage.Length - i));
                        writer.WriteLine($"~chunk~{chunk}");
                    }
                    // Отправляем конец передачи
                    writer.WriteLine("~end~");
                }
                catch (IOException ex)
                {
                    Logger.Instance.Log($"Error sending message to client {clientId}: {ex.Message}", this);
                }
                catch (Exception e)
                {
                    Logger.Instance.Log($"Error: {e}", this);
                }
            }
        }

        static object lockObject = new object();

        public void SendMessageToClient(params string[] args)
        {
            lock (lockObject)
            {
                if (writer == null)
                {
                    Logger.Instance.Log("StreamWriter is not initialized.", this);
                    return;
                }

                StringBuilder message = new StringBuilder();
                foreach (string command in args)
                {
                    message.Append($"{command}~sp~");
                }
                message.Remove(message.Length - 4, 4); // Удаление последнего "~sp~"

                string encryptedMessage = EncryptMessage(message.ToString());
                int chunkSize = 32768; // Размер чанка, например, 1024 символа

                try
                {
                    // Отправляем сообщение чанками
                    for (int i = 0; i < encryptedMessage.Length; i += chunkSize)
                    {
                        string chunk = encryptedMessage.Substring(i, Math.Min(chunkSize, encryptedMessage.Length - i));
                        writer.WriteLine($"~chunk~{chunk}");
                    }
                    // Отправляем конец передачи
                    writer.WriteLine("~end~");
                }
                catch (IOException ex)
                {
                    Logger.Instance.Log($"Error sending message to client {clientId}: {ex.Message}", this);
                }
                catch (Exception e)
                {
                    Logger.Instance.Log($"Error: {e}", this);
                }
            }
        }

        public void AddObserver(IClientMessageObserver observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
        }

        public void RemoveObserver(IClientMessageObserver observer)
        {
            if (observers.Contains(observer))
            {
                observers.Remove(observer);
            }
        }

        public bool HandleMessageFromClient(string data)
        {
            foreach (var observer in observers)
            {
                if (observer.HandleMessageFromClient(data))
                    return true;
            }

            string[] parts = data.Split("~sp~");
            string command = parts[0];
            string[] args = parts.Skip(1).ToArray();

            switch (command.ToLower())
            {
                case "reg":
                    HandleRegistration(args);
                    return true;
                case "auth":
                    HandleAuthentication(args);
                    return true;
                case "getimagebyid":
                    _ = HandleRequestToLoadImages(args);
                    return true;
                default:
                    Logger.Instance.Log($"Ошибка: Неизвестная команда '{command}'.", this);
                    return false;
            }
        }

        private async Task HandleRequestToLoadImages(string[] args)
        {
            FileData? imageFile = null;
            if (args.Length == 1 && Server.ImageService != null && Server.DatabaseController != null)
            {
                imageFile = await Server.DatabaseController.GetFileDataByKeyAsync(args[0]);
            }

            if (imageFile != null)
            {
                List<FileData> images = new List<FileData>
                {
                    imageFile
                };
                //await Task.Delay(2000);
                SendImages(images);
            }
        }

        private void SendImages(List<FileData> images)
        {
            if (Server.DatabaseController == null || Server.ImageService == null)
                return;
            string? strImage;
            List<string> args = new List<string>();
            foreach (FileData image in images)
            {
                strImage = Server.ImageService.GetFileInStringFormat(image);
                args.Add($"{image.FileKey}:key:{strImage}");
            }
            SendMessageToClient("loadimages", args);
            return;
        }

        private async void HandleRegistration(string[] args)
        {
            if (CurrentUser != null)
                return;
            if (args.Length != 2)
                return;

            string login = args[0];
            string password = args[1];

            // Базовые проверки
            if (login.Contains(' ') || password.Contains(' '))
            {
                return;
            }
            if (ContainsCyrillic(login) || ContainsCyrillic(password))
            {
                return;
            }
            if (!IsValidLogin(login))
            {
                return;
            }

            if (Server.DatabaseController == null)
                return;
            bool loginIsTaken = await Server.DatabaseController.IsLoginTakenAsync(login);
            if (loginIsTaken)
                SendMessageToClient("reg_result", "login_is_taken");
            else
            {
                string? avatarId = await Server.DatabaseController.GetDefaultUserAvatarKeyAsync();
                bool added = await Server.DatabaseController.AddEntityAsync(CurrentUser = new User { Login = login, PasswordHash = HashPassword(password), FixedKey = clientId, RoleId = 4, AvatarId = avatarId ?? string.Empty });
                if (added)
                    SendMessageToClient("reg_result", "completed");
                else
                    SendMessageToClient("reg_result", "internal_error");
            }
        }

        private async void HandleAuthentication(string[] args)
        {
            if (CurrentUser != null)
                return;
            if (args.Length != 2)
                return;

            string login = args[0];
            string password = args[1];

            // Базовые проверки
            if (login.Contains(' ') || password.Contains(' '))
            {
                return;
            }
            if (ContainsCyrillic(login) || ContainsCyrillic(password))
            {
                return;
            }
            if (!IsValidLogin(login))
            {
                return;
            }

            if (Server.DatabaseController != null)
            {
                try
                {
                    User? user = await Server.DatabaseController.GetUserByLoginAsync(login);
                    string nullId = "-1";
                    if (user != null)
                    {
                        bool correctPass = ValidatePassword(password, user.PasswordHash);
                        if (correctPass)
                        {
                            AddObserver(new AuthClientHandler(this, user));
                            SendMessageToClient("auth_result", "allowed", user.RoleId.ToString(), user.Id.ToString());
                            CurrentUser = user;
                        }
                        else
                            SendMessageToClient("auth_result", "incorrect_pass", nullId, nullId);
                    }
                    else
                        SendMessageToClient("auth_result", "incorrect", nullId, nullId);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Ошибка при авторизации клиента '{clientId}': {ex.Message}", this);
                }
            }
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                byte[] hashBytes = sha256.ComputeHash(passwordBytes);

                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }

        public static bool ValidatePassword(string enteredPassword, string storedHash)
        {
            string enteredHash = HashPassword(enteredPassword);
            return string.Equals(enteredHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsCyrillic(string text)
        {
            foreach (char c in text)
            {
                if (c >= 'А' && c <= 'я' || c == 'Ё' || c == 'ё')
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsValidLogin(string login)
        {
            foreach (char c in login)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                {
                    return false;
                }
                if (c >= 'А' && c <= 'я' || c == 'Ё' || c == 'ё')
                {
                    return false;
                }
            }
            return true;
        }

        private string EncryptMessage(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(messageBytes, 0, messageBytes.Length);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private string DecryptMessage(string encryptedMessage)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedMessage);
            using (MemoryStream ms = new MemoryStream(encryptedBytes))
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    using (StreamReader reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private DateTime getDateTimeClientConnection()
        {
            string timestamp = clientId.Substring(Math.Max(0, clientId.Length - 13));
            return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestamp)).LocalDateTime;
        }

        private void HandleClientDisconnect()
        {
            Logger.Instance.Log($"Client {remoteEndPoint} disconnected.", this);
        }

        public void Dispose() { }
    }
}