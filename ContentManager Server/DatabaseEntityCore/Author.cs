using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class Author
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Country { get; set; } = string.Empty;

        [JsonIgnore]
        public ICollection<Novel> Novels { get; set; }
    }
}
