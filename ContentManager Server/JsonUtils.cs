using ContentManager_Server.DatabaseEntityCore;
using Newtonsoft.Json;

namespace ContentManager_Server
{
    public static class JsonUtils
    {
        public static string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.None);
        }

        public static string UsersToJsonByOwnerRequest(List<User> users)
        {
            var selectedUsers = new List<object>();

            foreach (User user in users)
            {
                var userObject = new
                {
                    user.Id,
                    RoleName = user.Role.Name,
                    user.Login,
                    user.Nickname,
                    user.AvatarId
                };

                selectedUsers.Add(userObject);
            }

            return ToJson(selectedUsers);
        }

        public static string AuthorsToJsonByUserRequest(List<Author> authors)
        {
            var selectedUsers = new List<object>();

            foreach (Author author in authors)
            {
                var userObject = new
                {
                    author.Id,
                    author.Name,
                    author.Country
                };

                selectedUsers.Add(userObject);
            }

            return ToJson(selectedUsers);
        }

        public static string NovelsToJsonByUserRequest(List<Novel> novels)
        {
            var selectedNovels = new List<object>();

            foreach (Novel novel in novels)
            {
                var userObject = new
                {
                    novel.Id,
                    novel.Title,
                    novel.CreationDate,
                    novel.ChapterCount,
                    novel.AuthorId,
                    novel.Chapters
                };

                selectedNovels.Add(userObject);
            }

            return ToJson(selectedNovels);
        }

        public static string MessagesToJsonByUserRequest(List<Message> messages)
        {
            var selectedMessages = new List<object>();

            foreach (Message message in messages)
            {
                var userObject = new
                {
                    message.Id,
                    message.ChapterId,
                    message.Content,
                    message.SenderName,
                    message.MessageTypeId
                };

                selectedMessages.Add(userObject);
            }

            return ToJson(selectedMessages);
        }

        public static List<Chapter> JsonNovelChaptersToChaptersList(string json)
        {
            var list = JsonConvert.DeserializeObject<List<Chapter>>(json);
            if (list != null)
                return list;
            else
                return new List<Chapter>();
        }
    }
}
