using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.DTOs_Admin
{
    public class LoginModelAdmin
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
