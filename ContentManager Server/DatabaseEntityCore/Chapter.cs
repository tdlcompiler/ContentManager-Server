using System.ComponentModel.DataAnnotations;


namespace ContentManager_Server.DatabaseEntityCore
{
    public class Chapter
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public int NovelId { get; set; }
        public Novel Novel { get; set; }

        public ICollection<Message> Messages { get; set; }
    }
}
