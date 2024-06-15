using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;


namespace ContentManager_Server.DatabaseEntityCore
{
    public class Chapter
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [JsonIgnore]
        public int NovelId { get; set; }

        [JsonIgnore]
        public Novel Novel { get; set; }

        [JsonIgnore]
        public ICollection<Message> Messages { get; set; }
    }
}
