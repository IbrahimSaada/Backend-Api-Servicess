using System;
using System.Collections.Generic;

namespace Backend_Api_services.Models.DTOs
{
    public class StoriesResponse
    {
        public int story_id { get; set; }
        public int user_id { get; set; }
        public DateTime createdat { get; set; }
        public DateTime expiresat { get; set; }
        public bool isactive { get; set; }
        public int viewscount { get; set; }
        public bool isviewed { get; set; }
        public string fullname { get; set; } = ""; // Add fullname field
        public string profile_pic { get; set; } = ""; // Add profile_pic field
        // List of associated media details
        public List<StoriesMediaResponse> Media { get; set; } = new List<StoriesMediaResponse>();
    }

    public class StoriesMediaResponse
    {
        public int media_id { get; set; } 
        public string media_url { get; set; }
        public string media_type { get; set; }
        public DateTime expiresat { get; set; }  // Reflect the expiration time of the individual media
    }
}
