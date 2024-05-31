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
            var connectionString = $"Server={server};Database={database};Uid={uid};Pwd={password};";
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(5, 7, 39)));
            optionsBuilder.EnableSensitiveDataLogging();

            _contextManager = new DbContextManager(optionsBuilder.Options);
            Logger.Instance.Log("DatabaseController started.", this);
        }

        public async Task NormalizeTablesAsync()
        {
            try
            {
                var requiredRoles = new List<UserRole>
            {
                new UserRole { Name = "Владелец", Description = "Owner role", Id = (int)UserType.OWNER },
                new UserRole { Name = "Администратор", Description = "Administrator role", Id = (int)UserType.ADMINISTRATOR },
                new UserRole { Name = "Редактор", Description = "Editor role", Id = (int)UserType.EDITOR },
                new UserRole { Name = "Простой пользователь", Description = "Read-only user role", Id = (int)UserType.RO_USER }
            };

                foreach (var role in requiredRoles)
                {
                    using (var context = _contextManager.CreateDbContext())
                    {
                        var existingRole = await GetEntityByIdAsync<UserRole>(role.Id, context);
                        if (existingRole == null)
                        {
                            await context.UserRole.AddAsync(role);
                        }
                        else
                        {
                            existingRole.Description = role.Description;
                            context.UserRole.Update(existingRole);
                        }
                        await context.SaveChangesAsync();
                    }
                }

                var requiredFileTypes = new List<FileType>
            {
                new FileType { Name = "Изображения", Description = "Image files", Extension = "png;gif", Id = (int)TypeFile.IMAGE },
                new FileType { Name = "Префабы", Description = "Unity-Prefab files", Extension = "prefab", Id = (int)TypeFile.PREFAB }
            };

                foreach (var type in requiredFileTypes)
                {
                    using (var context = _contextManager.CreateDbContext())
                    {
                        var existingType = await GetEntityByIdAsync<FileType>(type.Id, context);
                        if (existingType == null)
                        {
                            await context.FileType.AddAsync(type);
                        }
                        else
                        {
                            existingType.Description = type.Description;
                            context.FileType.Update(existingType);
                        }
                        await context.SaveChangesAsync();
                    }
                }

                Logger.Instance.Log("All required tables have been normalized successfully.", this);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log("Error: " + ex.Message, this);
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

        public async Task<User?> GetUserByLoginAsync<T>(string login) where T : class
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
                        Logger.Instance.Log($"Entity {typeof(T).Name} with Login {login} retrieved successfully.", this);
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
                        await context.SaveChangesAsync();

                        Logger.Instance.Log($"Entity {typeof(T).Name} updated successfully.", this);
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

        public async Task<List<T>?> GetAllEntitiesAsync<T>(ApplicationDbContext context) where T : class
        {
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

    public enum TypeFile
    {
        NULL,
        IMAGE,
        PREFAB
    }
}