using ContentManager_Server.DatabaseEntityCore;

namespace ContentManager_Server
{
    public class AuthClientHandler : BaseModule, IClientMessageObserver
    {
        private User currentUser;
        private ClientHandler clientHandler;

        public AuthClientHandler(ClientHandler client, User user) : base($"ClientHandler '{user.Login}'")
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            clientHandler = client;
            client.AddObserver(this);

            FirstInit();
        }

        private void FirstInit()
        {
            clientHandler.SetCurrentUser(currentUser);
        }

        public bool HandleMessageFromClient(string data)
        {
            string[] parts = data.Split('~');
            string command = parts[0];
            string[] args;
            args = parts.Skip(1).ToArray();

            switch (command.ToLower())
            {
                case "getuserinfo":
                    HandleGetUserInfo();
                    return true;
                case "getserverinfo":
                    HandleGetServerInfo();
                    return true;
                case "geteditprofileinfo":
                    _ = HandleEditProfileMenuInfo();
                    return true;
                case "updateuseravatar":
                    _ = UpdateUserAvatar(args);
                    return true;
                default:
                    return false;
            }
        }

        private void HandleGetServerInfo()
        {
            SendMessageToClient("updateserverinfo", Server.GetCountActiveUsers().ToString());
        }

        private async Task UpdateUserAvatar(string[] args)
        {
            if (args.Length > 0 && Server.DatabaseController != null && Server.ImageService != null)
            {
                currentUser.AvatarId = args[0];
                await Server.DatabaseController.UpdateEntityAsync(currentUser, currentUser.Id);
                HandleGetUserInfo();
                SendMessageToClient("seteditprofiledata", currentUser.Nickname, currentUser.Role.Name, currentUser.AvatarId);
            }
        }

        private async Task<bool> HandleEditProfileMenuInfo()
        {
            if (Server.DatabaseController == null || Server.ImageService == null)
                return false;
            List<FileData>? images = await Server.DatabaseController.GetFilesByFileTypeAndDescAsync(TypeFile.IMAGE, "Avatar Image File");
            if (images == null)
                return false;

            List<string> args = new List<string>();
            foreach (FileData image in images)
            {
                args.Add(image.FileKey);
            }
            try
            {
                SendMessageToClient("setuseravatarimages", args);
                SendMessageToClient("seteditprofiledata", currentUser.Nickname, currentUser.Role.Name, currentUser.AvatarId);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString());
            }
            return true;
        }

        private bool HandleGetUserInfo()
        {
            if (Server.ImageService != null)
            {
                string imageId = currentUser.AvatarId;
                SendMessageToClient(".png", imageId, "update_user_avatar");
            }
            SendMessageToClient("setuserinfo", currentUser.Nickname, currentUser.Role.Name);
            return true;
        }

        private void SendMessageToClient(string command, List<string> args)
        {
            clientHandler.SendMessageToClient(command, args);
        }

        private void SendMessageToClient(params string[] args)
        {
            clientHandler.SendMessageToClient(args);
        }

        public void Dispose()
        {

        }
    }
}
