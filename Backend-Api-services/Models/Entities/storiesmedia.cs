using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend_Api_services.Models.Entities
{
    [Table("storiesmedia")]
    public class storiesmedia
    {
        [Key]
        public int media_id { get; set; }

        public int story_id { get; set; }

        public string media_url { get; set; }

        public string media_type { get; set; }

        public DateTime expiresat { get; set; } = DateTime.UtcNow.AddDays(1);

        [ForeignKey("story_id")]
        [JsonIgnore]  // Prevent cyclical references
        public stories stories { get; set; }
    }
}
