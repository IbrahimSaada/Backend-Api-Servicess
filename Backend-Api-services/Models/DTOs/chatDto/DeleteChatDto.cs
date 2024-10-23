namespace Backend_Api_services.Models.DTOs.chatDto
{
    public class DeleteChatDto
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }  // User requesting the deletion
    }
}
