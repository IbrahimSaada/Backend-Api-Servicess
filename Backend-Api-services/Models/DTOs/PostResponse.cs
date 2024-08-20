using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.DTOs
{
    public class PostResponse
    {
        [Key]
        public int post_id { get; set; }
        public int user_id { get; set; }
        public string caption { get; set; } = "";
        public DateTime created_at { get; set; }
        public bool is_public { get; set; } = true;
        public int like_count { get; set; } = 0;
        public int comment_count { get; set; } = 0;
        public string profile_pic { get; set; } = "";
        public string fullname { get; set; } = "";

        // List to hold associated media
        public List<PostMediaResponse> Media { get; set; } = new List<PostMediaResponse>();
        public bool is_liked { get; set; }
    }
}

