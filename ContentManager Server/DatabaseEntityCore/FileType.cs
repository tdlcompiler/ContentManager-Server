using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class FileType
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public string Extension { get; set; }

        public ICollection<FileData> Files { get; set; }
    }
}
