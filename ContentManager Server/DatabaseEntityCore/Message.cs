using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public string Text { get; set; }

        public int ChapterId { get; set; }
        public Chapter Chapter { get; set; }

        public int MessageTypeId { get; set; }
        public MessageType MessageType { get; set; }
    }
}
