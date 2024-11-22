namespace Backend_Api_services.Models.DTOs.NotificationDTO
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public int RecipientUserId { get; set; }
        public int? SenderUserId { get; set; }
        public string Type { get; set; }
        public int? RelatedEntityId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

}
