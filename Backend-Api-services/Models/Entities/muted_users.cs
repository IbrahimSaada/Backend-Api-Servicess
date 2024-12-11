using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("muted_users")]
    public class muted_users
    {
        [Key]
        public int muted_id { get; set; }
        public int muted_by_user_id { get; set; }
        public int muted_user_id { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;
        [ForeignKey("muted_by_user_id")]
        public Users MutedByUserId { get; set; }
        [ForeignKey("muted_user_id")]
        public Users UserById { get; set; }
    }
}
