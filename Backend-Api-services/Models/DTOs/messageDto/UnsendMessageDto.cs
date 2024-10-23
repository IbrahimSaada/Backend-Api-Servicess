namespace Backend_Api_services.Models.DTOs.messageDto
{
    public class UnsendMessageDto
    {
        public int MessageId { get; set; }
        public int UserId { get; set; }  // The user who wants to unsend
    }
}
