using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entites_Admin
{
    [Table("admins")]
    public class Admin
    {
        [Key]
        public int admin_id { get; set; }  // Corresponds to admin_id in the database

        public string username { get; set; }  // Corresponds to username in the database

        public string email { get; set; }  // Corresponds to email in the database

        public string password { get; set; }  // Corresponds to password in the database
    }
}
