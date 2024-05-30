using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class Author
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Country { get; set; }

        public ICollection<Novel> Novels { get; set; }
    }
}
