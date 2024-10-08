namespace Backend_Api_services.Models.DTOs
{
    public class UpdateFollowStatusDto
    {
        public int followed_user_id { get; set; } // The user being followed
        public int follower_user_id { get; set; } // The user who initiated the follow request
        public string approval_status { get; set; } // The new approval status (approved, declined, pending)
    }
}
