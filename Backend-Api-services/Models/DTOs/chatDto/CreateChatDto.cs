namespace Backend_Api_services.Models.DTOs.chatDto
{
    public class CreateChatDto
    {
        public int InitiatorUserId { get; set; }
        public int RecipientUserId { get; set; }
    }
}
