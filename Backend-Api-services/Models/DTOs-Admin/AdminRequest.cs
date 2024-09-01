using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.DTOs_Admin
{
    public class AdminRequest
    {
        //public string username { get; set; }

        [Required]
        [EmailAddress]
        public string email { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Password must be at least 8 characters long, contain at least one uppercase letter, one number, and one special character.")]
        public string password { get; set; }

        public string role { get; set; } = "admin";
    }
}
