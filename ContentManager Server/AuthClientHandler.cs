using ContentManager_Server.DatabaseEntityCore;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Tls;

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

        private void FirstInit() =>
            clientHandler.SetCurrentUser(currentUser);

        public bool HandleMessageFromClient(string data)
        {
            string[] parts = data.Split("~sp~");
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
                case "edituser":
                    HandleEditUser(args);
                    return true;
                case "geteditprofileinfo":
                    _ = HandleEditProfileMenuInfo(args);
                    return true;
                case "updateuseravatar":
                    _ = UpdateUserAvatar(args);
                    return true;
                case "getusers":
                    _ = HandleGetUsers(args);
                    return true;
                case "saveauthor":
                    HandleSaveAuthor(args);
                    return true;
                case "removeauthor":
                    HandleRemoveAuthor(args);
                    return true;
                case "savenovel":
                    HandleSaveNovel(args);
                    return true;
                case "removenovel":
                    HandleRemoveNovel(args);
                    return true;
                case "getauthorlist":
                    _ = HandleGetAuthorList(args);
                    return true;
                case "getnovellist":
                    _ = HandleGetNovelList(args);
                    return true;
                case "getmessages":
                    _ = HandleGetMessagesList(args);
                    return true;
                case "savemessage":
                    HandleSaveMessage(args);
                    return true;
                case "logout":
                    HandleLogout();
                    return true;
                default:
                    return false;
            }
        }

        private void HandleLogout()
        {
            clientHandler.ResetCurrentUser();
            clientHandler.RemoveObserver(this);

            SendMessageToClient("logoutconfirm");
        }

        private async void HandleSaveMessage(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR)) && Server.DatabaseController != null)
            {
                Message? message;

                if (args.Length > 0 && UserIs(UserType.ADMINISTRATOR))
                {
                    message = JsonConvert.DeserializeObject<Message>(args[0]);
                    if (message == null)
                        return;

                    if (Server.DatabaseController != null)
                    {
                        if (Server.ImageService != null && message.MessageTypeId == 2) // Изображение
                        {
                            message.Content = await Server.ImageService.AddFileAsync(message.Content);
                            FileData? file = await Server.DatabaseController.GetFileDataByKeyAsync(message.Content);

                            if (file == null)
                                return;
                        }
                        else if (Server.PrefabService != null && message.MessageTypeId == 3) // Префаб
                        {
                            message.Content = await Server.PrefabService.AddFileAsync(message.Content);
                            FileData? file = await Server.DatabaseController.GetFileDataByKeyAsync(message.Content);

                            if (file == null)
                                return;
                        }
                    }
                    if (message.Id == -1 && Server.DatabaseController != null)
                    {
                        bool added = await Server.DatabaseController.AddEntityAsync(message = new Message
                        {
                            ChapterId = message.ChapterId,
                            MessageTypeId = message.MessageTypeId,
                            SenderName = message.SenderName,
                            Content = message.Content
                        });

                        if (added)
                        {
                            string jsonstr = JsonUtils.MessagesToJsonByUserRequest(new List<Message> { message });
                            string command = "addmessage";
                            Server.SendMessageToAuthClients(UserType.ADMINISTRATOR, command, jsonstr);
                            Server.SendMessageToAuthClients(UserType.EDITOR, command, jsonstr);
                            Server.SendMessageToAuthClients(UserType.RO_USER, command, jsonstr);
                        }
                    }
                    else
                    {
                        //
                    }
                }
            }
        }

        private async Task<bool> HandleGetMessagesList(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR) || UserIs(UserType.RO_USER)) && Server.DatabaseController != null && args.Length == 3 && int.TryParse(args[0], out int chapterId) && int.TryParse(args[1], out int startIndex) && int.TryParse(args[2], out int endIndex))
            {
                List<Message>? messagesInRange = await Server.DatabaseController.GetChapterMessagesInRange(chapterId, startIndex, endIndex);

                if (messagesInRange != null && messagesInRange.Any())
                {
                    string messagesInJson = JsonUtils.MessagesToJsonByUserRequest(messagesInRange);
                    SendMessageToClient("setmessages", messagesInJson);
                    return true;
                }
                else
                {
                    SendMessageToClient("setmessages", "[]");
                }
            }
            return false;
        }

        private async Task<bool> HandleGetNovelList(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR) || UserIs(UserType.RO_USER)) && Server.DatabaseController != null && args.Length == 2 && int.TryParse(args[0], out int startIndex) && int.TryParse(args[1], out int endIndex))
            {
                List<Novel>? novelsInRange = await Server.DatabaseController.GetNovelsInRangeAsync(startIndex, endIndex);

                if (novelsInRange != null && novelsInRange.Any())
                {
                    string novelsInJson = JsonUtils.NovelsToJsonByUserRequest(novelsInRange);
                    SendMessageToClient("setnovels", novelsInJson);
                    return true;
                }
                else
                {
                    SendMessageToClient("setnovels", "[]");
                }
            }
            return false;
        }

        private async void HandleRemoveNovel(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR)) && Server.DatabaseController != null)
            {
                if (args.Length != 1)
                    return;
                if (int.TryParse(args[0], out int novelId))
                {
                    bool removed = await Server.DatabaseController.DeleteEntityAsync<Novel>(novelId);
                    if (removed)
                        SendMessageToClient("removenovel", args[0]);
                }
            }
        }

        private async void HandleSaveNovel(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR)) && Server.DatabaseController != null)
            {
                Novel? novel;

                if (args.Length == 4 && UserIs(UserType.ADMINISTRATOR))
                {
                    List<Chapter> chapters = JsonUtils.JsonNovelChaptersToChaptersList(args[3]);

                    if (HasDuplicateChapterTitles(chapters))
                    {
                        SendMessageToClient("errorsavenovel", "chaptersrepeat");
                        return;
                    }

                    bool added = await Server.DatabaseController.AddEntityAsync(novel = new Novel
                    {
                        Title = args[0],
                        AuthorId = int.Parse(args[1]),
                        CreationDate = DateTime.Parse(args[2]),
                        Chapters = chapters,
                        ChapterCount = chapters.Count
                    });

                    if (added)
                    {
                        string jsonstr = JsonUtils.NovelsToJsonByUserRequest(new List<Novel> { novel });
                        string command = "addnovel";
                        Server.SendMessageToAuthClients(UserType.ADMINISTRATOR, command, jsonstr);
                        Server.SendMessageToAuthClients(UserType.EDITOR, command, jsonstr);
                        Server.SendMessageToAuthClients(UserType.RO_USER, command, jsonstr);
                    }
                }
                else if (args.Length == 5)
                {
                    novel = await Server.DatabaseController.GetNovelByIdAsync(int.Parse(args[4]));
                    if (novel != null)
                    {
                        novel.Title = args[0];
                        novel.AuthorId = int.Parse(args[1]);
                        novel.CreationDate = DateTime.Parse(args[2]);

                        List<Chapter> chapters = JsonUtils.JsonNovelChaptersToChaptersList(args[3]);

                        if (HasDuplicateChapterTitles(chapters))
                        {
                            SendMessageToClient("errorsavenovel", "chaptersrepeat");
                            return;
                        }

                        novel.Chapters = chapters;
                        novel.ChapterCount = novel.Chapters.Count;

                        bool edited = await Server.DatabaseController.UpdateEntityAsync(novel, novel.Id);
                        if (edited)
                            SendMessageToClient("updatenovel", JsonUtils.NovelsToJsonByUserRequest(new List<Novel> { novel }));
                    }
                }
            }
        }

        private bool HasDuplicateChapterTitles(List<Chapter> chapters)
        {
            var duplicateTitles = chapters.GroupBy(chapter => chapter.Title)
                                          .Any(group => group.Count() > 1);

            return duplicateTitles;
        }


        private async void HandleEditUser(string[] args)
        {
            if (args.Length == 3 && Server.DatabaseController != null)
            {
                int userId = int.Parse(args[0]);
                string nickname = args[1];
                int roleId = int.Parse(args[2]);

                if (userId == currentUser.Id)
                {
                    currentUser.Nickname = nickname;
                    bool edited = await Server.DatabaseController.UpdateEntityAsync(currentUser, currentUser.Id);
                    if (edited)
                        HandleGetUserInfo();
                }
                else if (UserIs(UserType.OWNER))
                {
                    User? user = await Server.DatabaseController.GetUserByIdAsync(userId);
                    if (user != null)
                    {
                        user.RoleId = roleId;
                        user.Nickname = nickname;
                        user.Role = await Server.DatabaseController.GetUserRoleByIdAsync(roleId) ?? user.Role;

                        AuthClientHandler? authClientHandler = Server.GetAuthClientHandlerByUserId(userId);
                        if (authClientHandler != null)
                        {
                            authClientHandler.clientHandler.Disconnect();
                        }
                        await Server.DatabaseController.UpdateEntityAsync(user, user.Id);
                    }
                }
            }
        }

        private async void HandleRemoveAuthor(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR)) && Server.DatabaseController != null)
            {
                if (args.Length != 1)
                    return;
                if (int.TryParse(args[0], out int authorId))
                {
                    bool removed = await Server.DatabaseController.DeleteEntityAsync<Author>(authorId);
                    if (removed)
                        SendMessageToClient("removeauthor", args[0]);
                }
            }
        }

        private async Task<bool> HandleGetAuthorList(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR) || UserIs(UserType.RO_USER)) && Server.DatabaseController != null && args.Length == 2 && int.TryParse(args[0], out int startIndex) && int.TryParse(args[1], out int endIndex))
            {
                List<Author>? authorsInRange = await Server.DatabaseController.GetAuthorsInRangeAsync(startIndex, endIndex);

                if (authorsInRange != null && authorsInRange.Any())
                {
                    string authorsInJson = JsonUtils.AuthorsToJsonByUserRequest(authorsInRange);
                    SendMessageToClient("setauthors", authorsInJson);
                    return true;
                }
                else
                {
                    SendMessageToClient("setauthors", "[]");
                }
            }
            return false;
        }

        private async void HandleSaveAuthor(string[] args)
        {
            if ((UserIs(UserType.ADMINISTRATOR) || UserIs(UserType.EDITOR)) && Server.DatabaseController != null)
            {
                Author? author;
                if (args.Length == 2 && UserIs(UserType.ADMINISTRATOR))
                {
                    bool added = await Server.DatabaseController.AddEntityAsync(author = new Author { Name = args[0], Country = args[1] });
                    if (added)
                    {
                        string jsonstr = JsonUtils.AuthorsToJsonByUserRequest(new List<Author> { author });
                        string command = "addauthor";
                        Server.SendMessageToAuthClients(UserType.ADMINISTRATOR, command, jsonstr);
                        Server.SendMessageToAuthClients(UserType.EDITOR, command, jsonstr);
                        Server.SendMessageToAuthClients(UserType.RO_USER, command, jsonstr);
                    }
                }
                else if (args.Length == 3)
                {
                    author = await Server.DatabaseController.GetAuthorByIdAsync(int.Parse(args[2]));
                    if (author != null)
                    {
                        author.Name = args[0];
                        author.Country = args[1];
                        bool edited = await Server.DatabaseController.UpdateEntityAsync(author, author.Id);
                        if (edited)
                            SendMessageToClient("updateauthor", JsonUtils.AuthorsToJsonByUserRequest(new List<Author> { author }));
                    }
                }
            }
        }

        private async Task<bool> HandleGetUsers(string[] args)
        {
            if (UserIs(UserType.OWNER) && Server.DatabaseController != null && args.Length == 2 && int.TryParse(args[0], out int startIndex) && int.TryParse(args[1], out int endIndex))
            {
                List<User>? usersInRange = await Server.DatabaseController.GetUsersInRangeAsync(startIndex, endIndex);

                if (usersInRange != null && usersInRange.Any())
                {
                    string usersInJson = JsonUtils.UsersToJsonByOwnerRequest(usersInRange);
                    SendMessageToClient("setusers", usersInJson);
                    return true;
                }
                else
                {
                    SendMessageToClient("setusers", "[]");
                }
            }
            return false;
        }

        public bool UserIs(UserType userType)
        {
            return currentUser.RoleId == (int)userType;
        }

        private void HandleGetServerInfo()
        {
            SendMessageToClient("updateserverinfo", Server.GetCountActiveUsers().ToString());
        }

        private async Task UpdateUserAvatar(string[] args)
        {
            if (args.Length > 0 && Server.DatabaseController != null && Server.ImageService != null)
            {
                int userId = int.Parse(args[0]);

                if (currentUser.Id == userId)
                {
                    currentUser.AvatarId = args[1];
                    await Server.DatabaseController.UpdateEntityAsync(currentUser, currentUser.Id);
                    HandleGetUserInfo();
                    SendMessageToClient("seteditprofiledata", currentUser.Nickname, currentUser.Role.Name, currentUser.AvatarId);
                }
                else
                {
                    User? user = await Server.DatabaseController.GetUserByIdAsync(userId);
                    if (user == null)
                        return;

                    user.AvatarId = args[1];
                    AuthClientHandler? authClientHandler = Server.GetAuthClientHandlerByUserId(userId);
                    if (authClientHandler != null)
                    {
                        authClientHandler.clientHandler.Disconnect();
                    }
                    await Server.DatabaseController.UpdateEntityAsync(user, user.Id);
                    SendMessageToClient("seteditprofiledata", user.Nickname, user.Role.Name, user.AvatarId);
                }
            }
        }

        private async Task<bool> HandleEditProfileMenuInfo(string[] args)
        {
            if (Server.DatabaseController == null || Server.ImageService == null)
                return false;
            List<FileData>? images = await Server.DatabaseController.GetFilesByFileTypeAndDescAsync(TypeFile.IMAGE, "Avatar Image File");
            List<UserRole>? userRoles = await Server.DatabaseController.GetAllEntitiesAsync<UserRole>();
            if (images == null || userRoles == null)
                return false;

            List<string> argss = new List<string>();
            foreach (FileData image in images)
            {
                argss.Add(image.FileKey);
            }
            try
            {
                int userId = int.Parse(args[0]);
                if (userId == currentUser.Id)
                    SendMessageToClient("seteditprofiledata", currentUser.Nickname, currentUser.Role.Name, currentUser.AvatarId);
                else
                {
                    User? user = await Server.DatabaseController.GetUserByIdAsync(userId);

                    if (user != null)
                        SendMessageToClient("seteditprofiledata", user.Nickname, user.Role.Name, user.AvatarId);
                }
                SendMessageToClient("setroles", JsonUtils.ToJson(userRoles));
                SendMessageToClient("setuseravatarimages", argss);
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
