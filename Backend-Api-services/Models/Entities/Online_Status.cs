using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("online_status")]
    public class Online_Status
    {
        [Key]
        public int user_id { get; set; }  // Primary key and foreign key to Users table

        public bool is_online { get; set; } = false;  // Tracks if the user is online

        public DateTime? last_seen { get; set; }  // The last time the user was active

        // Foreign key relationship to Users table
        [ForeignKey("user_id")]
        public Users User { get; set; }  // Navigation property to the user
    }
}
