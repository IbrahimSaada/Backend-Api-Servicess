using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend_Api_services.Models.Entities
{
    [Table("storyviews")]
    public class storyviews
    {
        [Key]
        public int view_id { get; set; }
        public int story_id { get; set; }
        public int viewer_id { get; set; }
        public DateTime viewedat { get; set; } = DateTime.UtcNow;

        [ForeignKey("story_id")]
        [JsonIgnore]  // Prevent cyclical references
        public stories stories { get; set; }

        [ForeignKey("user_id")]
        [JsonIgnore]  // Prevent cyclical references
        public Users viewer { get; set; }
    }
}
