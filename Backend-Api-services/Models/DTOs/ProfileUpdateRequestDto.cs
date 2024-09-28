namespace Backend_Api_services.Models.DTOs
{
    public class ProfileUpdateRequestDto
    {
        public string? profile_pic { get; set; } // Profile picture URL or file path
        public string? fullname { get; set; }    // User's full name
        public string? bio { get; set; }         // User's bio/description
    }
}
