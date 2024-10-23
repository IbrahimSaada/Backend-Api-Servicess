namespace Backend_Api_services.Models.DTOs.chatDto
{
    public class ChatDto
    {
        public int ChatId { get; set; }
        public int InitiatorUserId { get; set; }
        public int RecipientUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
