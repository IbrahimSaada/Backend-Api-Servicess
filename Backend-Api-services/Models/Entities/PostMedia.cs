using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("postmedia")]
    public class PostMedia
    {
        [Key]
        public int media_id { get; set; }
        public string media_url { get; set; } = "";
        public string media_type { get; set; } = "";
        public int post_id { get; set; } // Foreign key to associate with Post

        // Navigation property to post
        [ForeignKey("post_id")]
        public Post post { get; set; }
    }
}
