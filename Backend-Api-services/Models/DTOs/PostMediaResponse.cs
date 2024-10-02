namespace Backend_Api_services.Models.DTOs
{
    public class PostMediaResponse
    {
        public int media_id { get; set; }
        public string media_url { get; set; } = "";
        public string media_type { get; set; } = "";
        public int post_id { get; set; } // Foreign key to associate with the Post
        public string? thumbnail_url { get; set; }
    }
}
