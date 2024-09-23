using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("followers")]
    public class Followers
    {
        [Key]
        public int follower_id { get; set; }

        public int user_id { get; set; }

        public int follower_user_id { get; set; }

        public bool is_public { get; set; }

        // Foreign key relationship to the Users table
        [ForeignKey("user_id")]
        public Users User { get; set; }

        // Foreign key relationship to the Users table for the follower
        [ForeignKey("follower_user_id")]
        public Users Follower { get; set; }
    }
}
