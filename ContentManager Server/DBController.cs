namespace ContentManager_Server
{
    using ContentManager_Server.DatabaseEntityCore;
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Data;

    public class DBController : BaseModule, IDisposable
    {
        private readonly DbContextManager _contextManager;

        public DBController(string server, string database, string uid, string password) : base("DatabaseController")
        {
            try
            {
                var connectionString = $"Server={server};Database={database};Uid={uid};Pwd={password};";
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(5, 7, 39)));
                optionsBuilder.EnableSensitiveDataLogging();

                using (var context = new ApplicationDbContext(optionsBuilder.Options))
                {
                    context.Database.OpenConnection();
                    context.Database.CloseConnection();
                }

                _contextManager = new DbContextManager(optionsBuilder.Options);
                Logger.Instance.Log($"DatabaseController {database} started.", this);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to connect to the database '{database}'. Please, edit 'server_config.json' and try again. Error: {ex.Message}", this);

                while (true)
                {
                    Thread.Sleep(Timeout.Infinite);
                }
            }
        }

        public async Task NormalizeTablesAsync()
        {
            var requiredRoles = new List<UserRole>
            {
                new UserRole { Name = "Владелец", Description = "Owner role", Id = (int)UserType.OWNER },
                new UserRole { Name = "Администратор", Description = "Administrator role", Id = (int)UserType.ADMINISTRATOR },
                new UserRole { Name = "Редактор", Description = "Editor role", Id = (int)UserType.EDITOR },
                new UserRole { Name = "Простой пользователь", Description = "Read-only user role", Id = (int)UserType.RO_USER }
            };

            var requiredFileTypes = new List<FileType>
            {
                new FileType { Name = "Изображения", Description = "Image files", Extension = "png;gif", Id = (int)TypeFile.IMAGE },
                new FileType { Name = "Префабы", Description = "Unity-Prefab files", Extension = "prefab", Id = (int)TypeFile.PREFAB }
            };

            var requiredMessageTypes = new List<MessageType>
            {
                new MessageType { Name = "Текстовое сообщение", Id = (int)TypeMessage.TEXT },
                new MessageType { Name = "Изображение", Id = (int)TypeMessage.IMAGE },
                new MessageType { Name = "Стикер", Id = (int)TypeMessage.STICKER }
            };

            try
            {
                using var context = _contextManager.CreateDbContext();
                await NormalizeEntitiesAsync(requiredRoles, context);
                await NormalizeEntitiesAsync(requiredFileTypes, context);
                await NormalizeEntitiesAsync(requiredMessageTypes, context);
                Logger.Instance.Log("All required tables have been normalized successfully.", this);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
            }
        }

        private async Task NormalizeEntitiesAsync<T>(List<T> requiredEntities, ApplicationDbContext context) where T : class
        {
            foreach (var entity in requiredEntities)
            {
                var existingEntity = await context.Set<T>().FindAsync(context.Entry(entity).Property("Id").CurrentValue);
                if (existingEntity == null)
                {
                    await context.Set<T>().AddAsync(entity);
                }
                else
                {
                    context.Entry(existingEntity).CurrentValues.SetValues(entity);
                }
                await context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsLoginTakenAsync(string login)
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    var isTaken = await context.User.AnyAsync(user => user.Login == login);
                    return isTaken;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return false;
            }
        }

        public async Task<List<FileData>?> GetAllFilesByFileTypeAsync(FileType type)
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    if (type != null)
                    {
                        var files = await context.FileData.Where(fd => fd.TypeId == type.Id).ToListAsync();
                        return files;
                    }
                    else
                    {
                        Logger.Instance.Log($"'{type.Name}' file type not found.", this);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving '{type.Name}' files: {ex.Message}", this);
                return null;
            }
        }

        public async Task<List<User>?> GetUsersInRangeAsync(int startIndex, int endIndex)
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.User
                                            .Where(u => u.Id >= startIndex && u.Id <= endIndex)
                                            .Select(u => new User
                                            {
                                                Id = u.Id,
                                                Login = u.Login ?? string.Empty,
                                                FixedKey = u.FixedKey ?? string.Empty,
                                                PasswordHash = u.PasswordHash ?? string.Empty,
                                                Nickname = u.Nickname ?? string.Empty,
                                                AvatarId = u.AvatarId ?? string.Empty,
                                                RoleId = u.Role.Id,
                                                Role = new UserRole
                                                {
                                                    Id = u.Role.Id,
                                                    Name = u.Role.Name ?? string.Empty,
                                                    Description = u.Role.Description ?? string.Empty
                                                }
                                            })
                                            .ToListAsync();
                Logger.Instance.Log($"Entities of type {typeof(User).Name} with Ids from {startIndex} to {endIndex} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<Author>?> GetAuthorsInRangeAsync(int startIndex, int endIndex)
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Author
                                            .Where(a => a.Id >= startIndex && a.Id <= endIndex)
                                            .Select(a => new Author
                                            {
                                                Id = a.Id,
                                                Name = a.Name ?? string.Empty,
                                                Country = a.Country ?? string.Empty
                                            })
                                            .ToListAsync();
                Logger.Instance.Log($"Entities of type {typeof(Author).Name} with Ids from {startIndex} to {endIndex} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<Message>?> GetChapterMessagesInRangeAsync(int chapterId, int startIndex, int endIndex)
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Message
                                            .Where(a => a.ChapterId == chapterId && a.Id >= startIndex && a.Id <= endIndex)
                                            .Select(a => new Message
                                            {
                                                Id = a.Id,
                                                ChapterId = a.ChapterId,
                                                Content = a.Content ?? string.Empty,
                                                MessageTypeId = a.MessageTypeId,
                                                SenderName = a.SenderName ?? string.Empty
                                            })
                                            .ToListAsync();
                Logger.Instance.Log($"Entities of type {typeof(Message).Name} with Ids from {startIndex} to {endIndex} and chapterId: {chapterId} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<Message>?> GetChapterMessagesAsync(int chapterId)
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Message
                                            .Where(a => a.ChapterId == chapterId)
                                            .Select(a => new Message
                                            {
                                                Id = a.Id,
                                                ChapterId = a.ChapterId,
                                                Content = a.Content ?? string.Empty,
                                                MessageTypeId = a.MessageTypeId,
                                                SenderName = a.SenderName ?? string.Empty
                                            })
                                            .ToListAsync();
                Logger.Instance.Log($"All entities of type {typeof(Message).Name} with chapterId: {chapterId} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<Novel>?> GetNovelsInRangeAsync(int startIndex, int endIndex)
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Novel
                                            .Include(n => n.Author)
                                            .Include(n => n.Chapters)
                                            .Where(n => n.Id >= startIndex && n.Id <= endIndex)
                                            .AsNoTracking()
                                            .ToListAsync();
                Logger.Instance.Log($"Entities of type {typeof(Novel).Name} with Ids from {startIndex} to {endIndex} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<Novel>?> GetAllNovelsAsync()
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Novel
                                            .Include(n => n.Author)
                                            .Include(n => n.Chapters)
                                            .AsNoTracking()
                                            .ToListAsync();
                Logger.Instance.Log($"All entities of type {typeof(Novel).Name} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<FileType?> GetFileTypeByTypeFile(TypeFile type)
        {
            using (var context = _contextManager.CreateDbContext())
            {
                return await GetEntityByIdAsync<FileType>((int)type, context);
            }
        }

        public async Task<FileData?> GetFileDataByKeyAsync(string fileKey)
        {
            using (var context = _contextManager.CreateDbContext())
            {
                return await context.FileData.Include(f => f.Type).FirstOrDefaultAsync(fd => fd.FileKey == fileKey);
            }
        }

        public async Task<Author?> GetAuthorByIdAsync(int authorId)
        {
            using (var context = _contextManager.CreateDbContext())
            {
                return await GetEntityByIdAsync<Author>(authorId, context);
            }
        }

        public async Task<Novel?> GetNovelByIdAsync(int novelId)
        {
            using (var context = _contextManager.CreateDbContext())
            {
                return await GetEntityByIdAsync<Novel>(novelId, context);
            }
        }

        public async Task<string?> GetDefaultUserAvatarKeyAsync()
        {
            try
            {
                var type = await GetFileTypeByTypeFile(TypeFile.IMAGE);
                using (var context = _contextManager.CreateDbContext())
                {
                    if (type != null)
                    {
                        var file = await context.FileData.Include(f => f.Type).FirstOrDefaultAsync(fd => fd.TypeId == type.Id && fd.FileDescription == "Default Avatar Image File");
                        return file?.FileKey;
                    }
                    else
                    {
                        Logger.Instance.Log($"'{type?.Name}' file type not found.", this);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving '{TypeFile.IMAGE}' files: {ex.Message}", this);
                return null;
            }
        }

        public async Task<FileData?> GetLoadingImageFileAsync()
        {
            try
            {
                var type = await GetFileTypeByTypeFile(TypeFile.IMAGE);
                using (var context = _contextManager.CreateDbContext())
                {
                    if (type != null)
                    {
                        var file = await context.FileData.Include(f => f.Type).FirstOrDefaultAsync(fd => fd.TypeId == type.Id && fd.FileDescription == "Loading gif file");
                        return file;
                    }
                    else
                    {
                        Logger.Instance.Log($"'{type?.Name}' file type not found.", this);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving '{TypeFile.IMAGE}' files: {ex.Message}", this);
                return null;
            }
        }

        public async Task<List<FileData>?> GetFilesByFileTypeAndDescAsync(TypeFile typef, string description)
        {
            try
            {
                var type = await GetFileTypeByTypeFile(typef);
                using (var context = _contextManager.CreateDbContext())
                {
                    if (type != null)
                    {
                        var files = await context.FileData.Where(fd => fd.TypeId == type.Id && fd.FileDescription == description).Include(fd => fd.Type).ToListAsync();
                        return files;
                    }
                    else
                    {
                        Logger.Instance.Log($"'{type?.Name}' file type not found.", this);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Error retrieving '{typef}' files: {ex.Message}", this);
                return null;
            }
        }

        public async Task<User?> GetUserByLoginAsync(string login)
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    var user = await context.User
                        .Include(u => u.Role)
                        .Where(u => u.Login == login)
                        .Select(u => new User
                        {
                            Id = u.Id,
                            Login = u.Login ?? string.Empty,
                            FixedKey = u.FixedKey ?? string.Empty,
                            PasswordHash = u.PasswordHash ?? string.Empty,
                            Nickname = u.Nickname ?? string.Empty,
                            AvatarId = u.AvatarId ?? string.Empty,
                            RoleId = u.Role.Id,
                            Role = new UserRole
                            {
                                Id = u.Role.Id,
                                Name = u.Role.Name ?? string.Empty,
                                Description = u.Role.Description ?? string.Empty,
                                Users = u.Role.Users
                            }
                        })
                        .FirstOrDefaultAsync();

                    if (user != null)
                    {
                        Logger.Instance.Log($"Entity {typeof(User).Name} with Login {login} retrieved successfully.", this);
                        return user;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    var user = await context.User
                        .Include(u => u.Role)
                        .Where(u => u.Id == id)
                        .Select(u => new User
                        {
                            Id = u.Id,
                            Login = u.Login ?? string.Empty,
                            FixedKey = u.FixedKey ?? string.Empty,
                            PasswordHash = u.PasswordHash ?? string.Empty,
                            Nickname = u.Nickname ?? string.Empty,
                            AvatarId = u.AvatarId ?? string.Empty,
                            RoleId = u.Role.Id,
                            Role = new UserRole
                            {
                                Id = u.Role.Id,
                                Name = u.Role.Name ?? string.Empty,
                                Description = u.Role.Description ?? string.Empty,
                                Users = u.Role.Users
                            }
                        })
                        .FirstOrDefaultAsync();

                    if (user != null)
                    {
                        Logger.Instance.Log($"Entity {typeof(User).Name} with Id {id} retrieved successfully.", this);
                        return user;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<UserRole?> GetUserRoleByIdAsync(int id)
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    UserRole? role = await GetEntityByIdAsync<UserRole>(id, context);

                    if (role != null)
                    {
                        Logger.Instance.Log($"Entity {typeof(UserRole).Name} with Id {id} retrieved successfully.", this);
                        return role;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<bool> AddEntityAsync<T>(T entity) where T : class
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    await context.Set<T>().AddAsync(entity);
                    await context.SaveChangesAsync();
                    Logger.Instance.Log($"Entity {typeof(T).Name} added successfully.", this);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return false;
            }
        }

        public async Task<bool> DeleteEntityAsync<T>(int id) where T : class
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    var entity = await context.Set<T>().FindAsync(id);
                    if (entity != null)
                    {
                        context.Set<T>().Remove(entity);
                        await context.SaveChangesAsync();
                        Logger.Instance.Log($"Entity {typeof(T).Name} with id {id} deleted successfully.", this);
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return false;
            }
        }

        public async Task<bool> UpdateEntityAsync<T>(T entity, object keyValue) where T : class
        {
            try
            {
                using (var context = _contextManager.CreateDbContext())
                {
                    var existingEntity = await context.Set<T>().FindAsync(keyValue);
                    if (existingEntity != null)
                    {
                        context.Entry(existingEntity).CurrentValues.SetValues(entity);

                        if (entity is Novel novel)
                        {
                            var existingNovel = existingEntity as Novel;
                            if (existingNovel != null)
                            {
                                context.Entry(existingNovel).Collection(n => n.Chapters).Load();
                                UpdateChapters(existingNovel.Chapters, novel.Chapters, context);
                            }
                        }

                        await context.SaveChangesAsync();

                        Logger.Instance.Log($"Entity {typeof(T).Name} updated successfully.", this);

                        if (entity is User user)
                            Server.SendUpdateUserToOwners(user);
                        else if (entity is Author author)
                            Server.SendUpdateAuthorToUsers(author);

                        return true;
                    }
                    else
                    {
                        Logger.Instance.Log($"Entity {typeof(T).Name} with key {keyValue} not found.", this);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return false;
            }
        }

        private void UpdateChapters(ICollection<Chapter> existingChapters, ICollection<Chapter> newChapters, DbContext context)
        {
            var existingChapterIds = existingChapters.Select(c => c.Id).ToList();
            var newChapterIds = newChapters.Select(c => c.Id).ToList();

            foreach (var existingChapter in existingChapters.Where(c => !newChapters.Any(nc => nc.Title == c.Title)).ToList())
            {
                context.Set<Chapter>().Remove(existingChapter);
            }

            foreach (var newChapter in newChapters)
            {
                var existingChapter = existingChapters.FirstOrDefault(c => c.Title == newChapter.Title);
                if (existingChapter != null)
                {
                    context.Entry(existingChapter).CurrentValues.SetValues(new Chapter { Title = newChapter.Title, Id = existingChapter.Id, NovelId = existingChapter.NovelId });
                }
                else
                {
                    existingChapters.Add(new Chapter { Title = newChapter.Title });
                }
            }
        }

        public async Task<T?> GetEntityByIdAsync<T>(int id, ApplicationDbContext context) where T : class
        {
            try
            {
                var entity = await context.Set<T>().FindAsync(id);
                if (entity != null)
                {
                    Logger.Instance.Log($"Entity {typeof(T).Name} with id {id} retrieved successfully.", this);
                }
                return entity;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public async Task<List<T>?> GetAllEntitiesAsync<T>() where T : class
        {
            using var context = _contextManager.CreateDbContext();
            try
            {
                var entities = await context.Set<T>().ToListAsync();
                Logger.Instance.Log($"All entities of type {typeof(T).Name} retrieved successfully.", this);
                return entities;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
                return null;
            }
        }

        public void Dispose()
        {
            _contextManager.Dispose();
        }
    }

    public enum UserType
    {
        NULL,
        OWNER,
        ADMINISTRATOR,
        EDITOR,
        RO_USER
    }

    public enum TypeMessage
    {
        NULL,
        TEXT,
        IMAGE,
        STICKER
    }

    public enum TypeFile
    {
        NULL,
        IMAGE,
        PREFAB
    }
}