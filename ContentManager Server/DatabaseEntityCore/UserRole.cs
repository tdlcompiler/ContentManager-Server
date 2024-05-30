using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class UserRole
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public ICollection<User> Users { get; set; }
    }
}
