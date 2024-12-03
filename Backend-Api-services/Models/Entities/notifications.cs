using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        public int notification_id { get; set; }
        public int recipient_user_id { get; set; }
        public int? sender_user_id { get; set; }
        public string type { get; set; }
        public int? related_entity_id { get; set; }
        public int? comment_id { get; set; }
        public string message { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        public bool is_read { get; set; } = false;
        public DateTime? last_push_sent_at { get; set; }
        public string aggregated_user_ids { get; set; }
    }
}
