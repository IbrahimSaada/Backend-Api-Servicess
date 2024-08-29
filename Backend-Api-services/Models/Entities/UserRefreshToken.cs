using Backend_Api_services.Models.Entites_Admin;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Api_services.Models.Entities
{
    [Table("userrefreshtokens")]
    public class UserRefreshToken
    {
        [Key]
        public int id { get; set; }

        // Nullable UserId to allow for Admin tokens
        [ForeignKey("User")]
        public int? userid { get; set; } // Nullable now

        // Nullable AdminId for admin tokens
        [ForeignKey("Admin")]
        public int? adminid { get; set; } // Newly added

        [Required]
        public string token { get; set; }

        [Required]
        public DateTime expiresat { get; set; }

        [Required]
        public DateTime createdat { get; set; } = DateTime.UtcNow;

        public Users User { get; set; }
        public Admin Admin { get; set; } // Newly added
    }
}
