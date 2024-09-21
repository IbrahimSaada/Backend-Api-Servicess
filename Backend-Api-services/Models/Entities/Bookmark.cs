using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend_Api_services.Models.Entities
{
    [Table("bookmark")]
    public class Bookmark
    {
        [Key]
        public int bookmark_id { get; set; }

        public int user_id { get; set; }
        public int post_id { get; set; }

        [ForeignKey("user_id")]
        [JsonIgnore]
        public Users users { get; set; }

        [ForeignKey("post_id")]
        [JsonIgnore]
        public Post post { get; set; }
    }
}
