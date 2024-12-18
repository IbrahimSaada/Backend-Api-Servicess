namespace Backend_Api_services.Models.DTOs
{
    public class LoginModel
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FcmToken { get; set; } // New property
    }
}
