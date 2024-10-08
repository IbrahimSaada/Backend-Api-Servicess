using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("followers")]
    public class Followers
    {
        [Key]
        public int follower_id { get; set; }

        public int followed_user_id { get; set; }

        public int follower_user_id { get; set; }

        public string approval_status { get; set; } = "pending";

        public bool is_dismissed { get; set; } = false;

        // Foreign key relationship to the Users table
        [ForeignKey("followed_user_id")]
        public Users User { get; set; }

        // Foreign key relationship to the Users table for the follower
        [ForeignKey("follower_user_id")]
        public Users Follower { get; set; }
    }
}
