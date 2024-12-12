using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("blocked_users")]
    public class BlockedUsers
    {
        [Key]
        public int block_id { get; set; }
        public int blocked_by_user_id { get; set; }
        public int blocked_user_id { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("blocked_by_user_id")]
        public Users BlockedByUser { get; set; }
        [ForeignKey("blocked_user_id")]
        public Users BlockedUser { get; set; }
    }
}
