using ContentManager_Server.DatabaseEntityCore;
using System.Text;

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
                    _ = HandleGetUserInfo();
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

        private async Task UpdateUserAvatar(string[] args)
        {
            if (args.Length > 0 && Server.DatabaseController != null && Server.ImageService != null)
            {
                currentUser.AvatarId = args[0];
                await Server.DatabaseController.UpdateEntityAsync(currentUser, currentUser.Id);
                await HandleGetUserInfo();
                string? strImage = await Server.ImageService.GetFileInStringFormatAsync(currentUser.AvatarId);
                SendMessageToClient($"seteditprofiledata~{currentUser.Nickname}~{currentUser.Role.Name}~{strImage}");
            }
        }

        private async Task<bool> HandleEditProfileMenuInfo()
        {
            if (Server.DatabaseController == null || Server.ImageService == null)
                return false;
            StringBuilder sb = new StringBuilder("setuseravatarimages");
            List<FileData>? images = await Server.DatabaseController.GetFilesByFileTypeAndDescAsync(TypeFile.IMAGE, "Avatar Image File");
            if (images == null)
                return false;
            string? strImage;
            foreach (FileData image in images)
            {
                strImage = Server.ImageService.GetFileInStringFormat(image);
                sb.Append($"~{image.FileKey}:{strImage}");
            }
            SendMessageToClient(sb.ToString());
            strImage = await Server.ImageService.GetFileInStringFormatAsync(currentUser.AvatarId);
            SendMessageToClient($"seteditprofiledata~{currentUser.Nickname}~{currentUser.Role.Name}~{strImage}");
            return true;
        }

        private async Task<bool> HandleGetUserInfo()
        {
            string userAvatar = string.Empty;
            string userInfo = $"setuserinfo~{currentUser.Nickname}~{currentUser.Role.Name}";
            if (Server.ImageService != null)
            {
                string? strImage = await Server.ImageService.GetFileInStringFormatAsync(currentUser.AvatarId);
                userAvatar = $".png~{strImage}~update_user_avatar";
            }
            SendMessageToClient(userInfo);
            SendMessageToClient(userAvatar);
            return true;
        }

        private void SendMessageToClient(string message)
        {
            clientHandler.SendMessageToClient(message);
        }

        public void Dispose()
        {

        }
    }
}
