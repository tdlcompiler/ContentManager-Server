using Microsoft.EntityFrameworkCore;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Author> Author { get; set; }
        public DbSet<Novel> Novel { get; set; }
        public DbSet<Chapter> Chapter { get; set; }
        public DbSet<MessageType> MessageType { get; set; }
        public DbSet<Message> Message { get; set; }
        public DbSet<UserRole> UserRole { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<FileType> FileType { get; set; }
        public DbSet<FileData> FileData { get; set; }
    }
}