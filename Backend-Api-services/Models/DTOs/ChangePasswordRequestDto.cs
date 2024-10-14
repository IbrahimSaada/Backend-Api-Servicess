namespace Backend_Api_services.Models.DTOs
{
    public class ChangePasswordRequestDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
