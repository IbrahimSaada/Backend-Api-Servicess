namespace Backend_Api_services.Models.DTOs.messageDto
{
    public class CreateMessageDto
    {
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string MessageType { get; set; }  // 'text', 'image', etc.
        public string MessageContent { get; set; }
        public List<string> MediaUrls { get; set; } = new List<string>();  // List of media URLs for attached media (optional)
    }
}
