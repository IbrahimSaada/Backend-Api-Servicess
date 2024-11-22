namespace Backend_Api_services.Models.DTOs
{
    public class LoginModel
    {
        public string? EmailOrPhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? FcmToken { get; set; } // New property
    }
}
