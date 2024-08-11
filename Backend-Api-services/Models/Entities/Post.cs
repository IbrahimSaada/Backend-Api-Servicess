using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("posts")] // Ensure this matches the table name in your database
    public class Post
    {
        [Key]
        public int post_id { get; set; }
        public int user_id { get; set; }
        public string caption { get; set; } = "";
        public DateTime created_at { get; set; }
        public bool is_public { get; set; } = true;
        public int like_count { get; set; } = 0;
        public int comment_count { get; set; } = 0;

        public List<PostMedia> Media { get; set; } = new List<PostMedia>();

        [ForeignKey("user_id")]
        public Users User { get; set; }
    }
}