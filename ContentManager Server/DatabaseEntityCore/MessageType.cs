using System.ComponentModel.DataAnnotations;

namespace ContentManager_Server.DatabaseEntityCore
{
    public class MessageType
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public ICollection<Message> Messages { get; set; }
    }
}
