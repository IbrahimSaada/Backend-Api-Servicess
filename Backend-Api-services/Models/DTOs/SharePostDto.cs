using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.DTOs
{
    public class SharePostDto
    {
        [Required]
        public int UserId { get; set; }  // The ID of the user sharing the post

        [Required]
        public int PostId { get; set; }  // The ID of the post being shared
        public string? Comment { get; set; }
    }
}
