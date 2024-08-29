using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("shared_posts")]
    public class shared_posts
    {
        [Key]
        [Column("share_id")]  // Explicitly map to the "share_id" column in the database
        public int ShareId { get; set; }

        [Column("user_id")]  // Explicitly map to the "sharer_id" column in the database
        [ForeignKey("Sharedby")]  // Map to the "Sharedby" navigation property
        public int SharerId { get; set; }

        [Column("post_id")]  // Explicitly map to the "post_id" column in the database
        [ForeignKey("PostContent")]  // Map to the "PostContent" navigation property
        public int PostId { get; set; }

        [Column("shared_at")]  // Explicitly map to the "shared_at" column in the database
        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
        [Column("comment")]  // Explicitly map to the "comment" column in the database
        public string? Comment { get; set; }  // New attribute for the optional comment
        public Users Sharedby { get; set; }  // Navigation property for Users

        public Post PostContent { get; set; }  // Navigation property for Posts
    }
}
