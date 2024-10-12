namespace Backend_Api_services.Models.DTOs
{
    public class PrivacySettingsRequest
    {
            public bool? IsPublic { get; set; }               // Optional: Profile visibility (public/private)
            public bool? IsFollowersPublic { get; set; }      // Optional: Followers list visibility
            public bool? IsFollowingPublic { get; set; }      // Optional: Following list visibility
    }
}
