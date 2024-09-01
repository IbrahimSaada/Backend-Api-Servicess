using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entites_Admin
{
    [Table("admins")]
    public class Admin
    {
        [Key]
        public int admin_id { get; set; }

        [Required]
        [EmailAddress]
        public string email { get; set; }  // Ensure email is unique

        [Required]
        public string password { get; set; }

        [Required]
        public string username { get; set; }  // This will be auto-generated

        [Required]
        public string role { get; set; } = "admin";
    }
}
