using System.Collections.Generic;

namespace Backend_Api_services.Models.DTOs
{
    public class PostRequest
    {
        public int user_id { get; set; }
        public string caption { get; set; } = "";
        public bool is_public { get; set; } = true;
        public List<PostMediaRequest> Media { get; set; } = new List<PostMediaRequest>();
    }

    public class PostMediaRequest
    {
        public string media_url { get; set; } = "";
        public string media_type { get; set; } = "";
    }
}