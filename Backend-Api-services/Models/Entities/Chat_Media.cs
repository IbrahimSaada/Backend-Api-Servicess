using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.Entities
{
    [Table("chat_media")]
    public class Chat_Media
    {
        [Key]
        public int media_id { get; set; }  // Unique identifier for each media item

        public int message_id { get; set; }  // Foreign key to the Messages table

        [ForeignKey("message_id")]
        public Messages Message { get; set; }  // Navigation property to the parent message

        public string media_url { get; set; }  // URL of the media file

        public string media_type { get; set; }  // 'image', 'video', 'audio', etc.
    }
}
