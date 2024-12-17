using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entites_Admin
{
    [Table("banned_users")]
    public class banned_users
    {
        [Key]
        public int ban_id { get; set; }
        public int user_id { get; set; }
        public string ban_reason { get; set; }
        public DateTime banned_at { get; set; } = DateTime.UtcNow;
        public DateTime? expires_at { get; set; }
        public bool is_active { get; set; } = true;

        [ForeignKey("user_id")]
        public Users users { get; set; }
    }
}
