namespace Backend_Api_services.Models.DTOs.OnlineStatusDTO
{
    public class UpdateOnlineStatusDto
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
    }
}
