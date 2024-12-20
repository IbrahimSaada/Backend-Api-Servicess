namespace Backend_Api_services.Models.DTOs_Admin
{
    public class BannedUserDto
    {
        public int UserId { get; set; }
        public string BanReason { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
