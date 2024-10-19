using FirebaseAdmin.Auth;

namespace Backend_Api_services.Models.DTOs.feedDto
{
    public class FeedItemResponse
    {
        public string Type { get; set; } // "post" or "repost"
        public int ItemId { get; set; } // PostId for posts, ShareId for shared posts
        public DateTime CreatedAt { get; set; } // Post creation time or share time

        // The user who created this feed item (poster or sharer)
        public UserInfo User { get; set; }

        // Main text content
        public string Content { get; set; } // For posts: caption; For reposts: sharer's comment

        // Original Post Details (for reposts only)
        public PostInfo Post { get; set; } // Includes original post details

        // User Interactions
        public bool IsLiked { get; set; }
        public bool IsBookmarked { get; set; }
    }


}
