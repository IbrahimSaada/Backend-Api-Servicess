using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("likes")]
    public class Like
    {
        [Key]
        public int like_id { get; set; }
        public int post_id { get; set; }
        public int user_id { get; set; }
        public DateTime created_at { get; set; } = DateTime.UtcNow;

        [ForeignKey("post_id")]
        public Post Post { get; set; }

        [ForeignKey("user_id")]
        public Users User { get; set; }
    }
}
