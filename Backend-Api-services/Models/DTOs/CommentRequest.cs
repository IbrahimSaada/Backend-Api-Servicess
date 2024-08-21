namespace Backend_Api_services.Models.DTOs
{
    public class CommentRequest
    {
        public int postid { get; set; }
        public int userid { get; set; }
        public int? parentcommentid { get; set; } // For replies
        public string text { get; set; }
    }
}