using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class Novel
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public DateTime CreationDate { get; set; }

        public int ChapterCount { get; set; }

        public int AuthorId { get; set; }
        public Author Author { get; set; }

        public ICollection<Chapter> Chapters { get; set; }
    }
}
