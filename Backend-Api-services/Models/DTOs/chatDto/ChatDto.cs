namespace Backend_Api_services.Models.DTOs.chatDto
{
    public class ChatDto
    {
        public int ChatId { get; set; }
        public int InitiatorUserId { get; set; }
        public string InitiatorUsername { get; set; }
        public string InitiatorProfilePic { get; set; }
        public int RecipientUserId { get; set; }
        public string RecipientUsername { get; set; }
        public string RecipientProfilePic { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime deleted_at_initiator { get; set; }
        public DateTime deleted_at_recipient { get; set; }
    }

}
