namespace Backend_Api_services.Models.DTOs
{
    public class SharedPostDetailsDto
    {
        public int ShareId { get; set; }  // The ID of the share action

        public int SharerId { get; set; }  // The ID of the user who shared the post
        public string SharerUsername { get; set; }  // The username of the user who shared the post
        public string SharerProfileUrl { get; set; }  // The profile URL of the user who shared the post

        public int PostId { get; set; }  // The ID of the shared post
        public string PostContent { get; set; }  // The content of the shared post
        public DateTime PostCreatedAt { get; set; }  // The timestamp when the post was created

        public List<PostMediaDto> Media { get; set; }  // A list of media associated with the post

        public DateTime SharedAt { get; set; }  // The timestamp when the post was shared
        public string? Comment { get; set; }  // Optional comment added by the sharer

        public string OriginalPostUserUrl { get; set; }  // The profile URL of the original post's author
        //for user profile
        public string OriginalPostFullName { get; set; }

        public int like_count { get; set; }

        public int comment_count { get; set; }

        public bool is_liked { get; set; }

        public bool is_Bookmarked { get; set; }
        //for user profile
    }

    public class PostMediaDto
    {
        public string MediaUrl { get; set; }  // The URL of the media
        public string MediaType { get; set; }  // The type of media (e.g., image, video)
        public string ThumbnailUrl { get; set; }
    }
}
