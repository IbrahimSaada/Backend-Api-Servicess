using System;
using System.Collections.Generic;

namespace Backend_Api_services.Models.DTOs
{
    public class CommentResponse
    {
        public int commentid { get; set; }
        public int postid { get; set; }
        public int userid { get; set; }
        public string fullname { get; set; }
        public string userprofilepic { get; set; }
        public string text { get; set; }
        public DateTime created_at { get; set; }
        public List<CommentResponse> Replies { get; set; } = new List<CommentResponse>();
        public CommentResponse ParentComment { get; set; }  // Add this property
        public bool isHighlighted { get; set; }
    }
}
