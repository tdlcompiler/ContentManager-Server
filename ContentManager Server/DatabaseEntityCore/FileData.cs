using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class FileData
    {
        public int Id { get; set; }

        [Required]
        public int TypeId { get; set; }
        public FileType Type { get; set; }

        [Required]
        public string FileKey { get; set; } = string.Empty;

        public string FileDescription { get; set; } = string.Empty;
    }
}
