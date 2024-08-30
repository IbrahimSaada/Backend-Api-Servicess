using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend_Api_services.Models.Entities
{
    [Table("stories")]
    public class stories
    {
        [Key]
        public int story_id { get; set; }

        public int user_id { get; set; }

        public DateTime createdat { get; set; } = DateTime.UtcNow;

        public DateTime expiresat { get; set; } = DateTime.UtcNow.AddDays(1);

        public bool isactive { get; set; } = true;

        public int viewscount { get; set; } = 0;

        public List<storiesmedia> Media { get; set; } = new List<storiesmedia>();

        [ForeignKey("user_id")]
        [JsonIgnore]  // Prevent cyclical references
        public Users users { get; set; }
    }
}
