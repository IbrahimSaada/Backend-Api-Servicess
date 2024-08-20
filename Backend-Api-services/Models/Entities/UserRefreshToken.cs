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

        [ForeignKey("User")]
        public int userid { get; set; }

        [Required]
        public string token { get; set; }

        [Required]
        public DateTime expiresat { get; set; }

        [Required]
        public DateTime createdat { get; set; } = DateTime.UtcNow;

        public Users User { get; set; }
    }
}
