using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class DbContextManager : IDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public DbContextManager(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(_options);
        }

        public void Dispose()
        {
            // DbContext is disposed in each operation, so nothing to dispose here.
        }
    }

}
