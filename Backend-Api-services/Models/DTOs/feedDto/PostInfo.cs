using FirebaseAdmin.Auth;

namespace Backend_Api_services.Models.DTOs.feedDto
{
    public class PostInfo
    {
        public int PostId { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserInfo Author { get; set; } // Original poster's info (only for reposts)
        public string Content { get; set; } // Original post's caption
        public List<PostMediaResponse> Media { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
    }


}
