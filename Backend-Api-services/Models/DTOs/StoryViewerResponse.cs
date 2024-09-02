using System;

namespace Backend_Api_services.Models.DTOs
{
    public class StoryViewerResponse
    {
        public int viewer_id { get; set; }
        public string fullname { get; set; }
        public string profile_pic { get; set; }
        public DateTime viewed_at { get; set; }
    }
}
