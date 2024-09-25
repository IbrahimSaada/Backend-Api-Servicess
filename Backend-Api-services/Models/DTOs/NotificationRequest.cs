namespace Backend_Api_services.Models.DTOs
{
    public class NotificationRequest
    {
        public string Token { get; set; }  // FCM Token of the recipient
        public string Title { get; set; }  // Notification title
        public string Body { get; set; }   // Notification body
    }
}
