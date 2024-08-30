using System.Collections.Generic;

namespace Backend_Api_services.Models.DTOs
{
    public class StoriesRequest
    {
        public int user_id { get; set; }

        public List<StoriesMediaRequest> Media { get; set; } = new List<StoriesMediaRequest>();
    }

    public class StoriesMediaRequest
    {
        public string media_url { get; set; } = "";
        public string media_type { get; set; } = "";
    }
}