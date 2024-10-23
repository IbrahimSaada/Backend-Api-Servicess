namespace Backend_Api_services.Models.DTOs.OnlineStatusDTO
{
    public class OnlineStatusDto
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}
