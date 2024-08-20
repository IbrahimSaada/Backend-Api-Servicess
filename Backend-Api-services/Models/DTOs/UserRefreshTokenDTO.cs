namespace Backend_Api_services.Models.DTOs
{
    public class UserRefreshTokenDTO
    {
        public int id { get; set; }
        public int userid { get; set; }
        public string token { get; set; }
        public DateTime expiresat { get; set; }
        public DateTime createdat { get; set; }
    }
}