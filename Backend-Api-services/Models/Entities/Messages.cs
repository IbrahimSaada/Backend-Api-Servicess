using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.Entities
{
    [Table("messages")]
    public class Messages
    {
        [Key]
        public int message_id { get; set; }

        public int chat_id { get; set; }

        [ForeignKey("chat_id")]
        public Chat Chats { get; set; }

        public int sender_id { get; set; }

        [ForeignKey("sender_id")]
        public Users Sender { get; set; }

        public string message_type { get; set; }

        public string message_content { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public DateTime? read_at { get; set; }

        public bool is_deleted_by_sender { get; set; }
        public bool is_deleted_by_recipient { get; set; }
        public bool is_unsent { get; set; }
        public bool is_edited { get; set; }

        public ICollection<Chat_Media> MediaItems { get; set; }  // Collection of media files associated with the message
    }
}
