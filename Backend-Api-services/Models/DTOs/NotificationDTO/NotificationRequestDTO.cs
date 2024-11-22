namespace Backend_Api_services.Models.DTOs.NotificationDTO
{
    public class NotificationRequest
    {
        public string Token { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }
}
