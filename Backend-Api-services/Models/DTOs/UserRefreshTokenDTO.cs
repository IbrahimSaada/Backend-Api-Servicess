namespace Backend_Api_services.Models.DTOs
{
    public class UserRefreshTokenDTO
    {
        public int id { get; set; }
        public int? userid { get; set; } // Nullable now
        public int? adminid { get; set; } // Newly added
        public string token { get; set; }
        public DateTime expiresat { get; set; }
        public DateTime createdat { get; set; }
    }
}