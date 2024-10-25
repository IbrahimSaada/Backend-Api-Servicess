namespace Backend_Api_services.Models.DTOs.messageDto
{
    public class MessageDto
    {
        public int MessageId { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string SenderUsername { get; set; }
        public string SenderProfilePic { get; set; }
        public string MessageType { get; set; }
        public string MessageContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsUnsent { get; set; }
        public List<string> MediaUrls { get; set; }
    }
}
