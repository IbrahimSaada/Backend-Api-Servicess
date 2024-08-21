using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("comments")]
    public class Comment
    {
        [Key]
        public int comment_id { get; set; }

        [Required]
        public int post_id { get; set; }

        [Required]
        public int user_id { get; set; }

        public int? parent_comment_id { get; set; } // For replies

        [Required]
        public string text { get; set; }

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("post_id")]
        public Post Post { get; set; }

        [ForeignKey("user_id")]
        public Users User { get; set; }

        [ForeignKey("parent_comment_id")]
        public Comment ParentComment { get; set; }
    }
}
