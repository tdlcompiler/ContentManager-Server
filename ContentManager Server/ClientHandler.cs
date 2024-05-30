using ContentManager_Server.DatabaseEntityCore;
using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ContentManager_Server
{
    public class ClientHandler : BaseModule, IClientMessageObserver
    {
        private readonly List<IClientMessageObserver> observers = new List<IClientMessageObserver>();
        private TcpClient client;
        private NetworkStream stream;
        private StreamWriter? writer;
        private Aes aes;
        private string clientId;
        private string remoteEndPoint;
        private User currentUser;

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

        public void SetCurrentUser(User user)
        {
            currentUser = user;
        }

        public void ResetCurrentUser()
        {
            currentUser = null;
        }

        public void HandleClient()
        {
            Logger.Instance.Log($"DateTime of {clientId} connection: {getDateTimeClientConnection():dd.MM.yyyy HH:mm:ss}", this);
            try
            {
                using (stream)
                {
                    using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine(EncryptMessage("Welcome to the server!"));
                    writer.WriteLine(EncryptMessage($"setloadingimage~{Server.ImageService?.GetFileInStringFormat(Server.ImageService.LoadingImage)}"));

                    string clientMessage;
                    while ((clientMessage = reader.ReadLine()) != null)
                    {
                        HandleMessageFromClient(DecryptMessage(clientMessage));
                        /* Logger.Instance.Log("Received: " + decryptedMessage, this);

                           string response = "Echo: " + decryptedMessage;
                           string encryptedResponse = EncryptMessage(response);
                           writer.WriteLine(encryptedResponse); */
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

        public void SendMessageToClient(string message)
        {
            if (writer == null)
            {
                Logger.Instance.Log("StreamWriter is not initialized.", this);
                return;
            }

            string encryptedMessage = EncryptMessage(message);

            try
            {
                writer.WriteLine(encryptedMessage);
            }
            catch (IOException ex)
            {
                Logger.Instance.Log($"Error sending message to client {clientId}: {ex.Message}", this);
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

            string[] parts = data.Split('~');
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
                default:
                    Logger.Instance.Log($"Ошибка: Неизвестная команда '{command}'.", this);
                    return false;
            }
        }

        private async void HandleRegistration(string[] args)
        {
            if (currentUser != null)
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

            bool loginIsTaken = await Server.DatabaseController.IsLoginTakenAsync(login);
            if (loginIsTaken)
                SendMessageToClient("reg_result~login_is_taken");
            else
            {
                bool added = await Server.DatabaseController.AddEntityAsync(currentUser = new User { Login = login, PasswordHash = HashPassword(password), FixedKey = clientId, RoleId = 4 });
                if (added)
                    SendMessageToClient("reg_result~completed");
                else
                    SendMessageToClient("reg_result~internal_error");
            }
        }

        private async void HandleAuthentication(string[] args)
        {
            if (currentUser != null)
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

            try
            {
                User user = await Server.DatabaseController.GetUserByLoginAsync<User>(login);
                if (user != null)
                {
                    bool correctPass = ValidatePassword(password, user.PasswordHash);
                    if (correctPass)
                    {
                        AddObserver(new AuthClientHandler(this, user));
                        SendMessageToClient($"auth_result~allowed~{user.RoleId}");
                        currentUser = user;
                    }
                    else
                        SendMessageToClient("auth_result~incorrect_pass~-1");
                }
                else
                    SendMessageToClient("auth_result~incorrect~-1");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Ошибка при авторизации клиента '{clientId}': {ex.Message}", this);
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
            // Hash the entered password
            string enteredHash = HashPassword(enteredPassword);

            // Compare the hashes
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