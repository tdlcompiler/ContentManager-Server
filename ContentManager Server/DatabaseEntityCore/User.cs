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
        public string FixedKey { get; set; } = string.Empty;

        [Required]
        public string Login { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string Nickname { get; set; } = string.Empty;

        public string AvatarId { get; set; } = string.Empty;
    }
}
