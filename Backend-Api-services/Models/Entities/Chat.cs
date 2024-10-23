using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.Entities
{
    [Table("chats")]
    public class Chat
    {
        [Key]
        public int chat_id { get; set; }

        public int user_initiator { get; set; }

        public int user_recipient { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public bool is_deleted_by_initiator { get; set; }

        public bool is_deleted_by_recipient { get; set; }

        [ForeignKey("user_initiator")]
        public Users InitiatorUser { get; set; }

        [ForeignKey("user_recipient")]
        public Users RecipientUser { get; set; }
    }
}
