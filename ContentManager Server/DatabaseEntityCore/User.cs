using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public int RoleId { get; set; }
        public UserRole Role { get; set; }

        [Required]
        public string FixedKey { get; set; }

        [Required]
        public string Login { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string Nickname { get; set; }

        public string AvatarId { get; set; }
    }
}
