namespace Backend_Api_services.Models.DTOs
{
    public class FollowUserDto
    {
        public int user_id { get; set; } // User initiating the follow request
        public int follower_user_id { get; set; } // User being followed
        public bool is_public { get; set; } = true; // If the following is public
    }
}
