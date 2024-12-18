namespace Backend_Api_services.Models.DTOs
{
    public class RegisterUserDto
    {
        public int user_id { get; set; }
        public string fullname { get; set; } = "";
        public string email { get; set; } = "";
        public string password { get; set; } = "";
        public DateTime dob { get; set; }
        public string gender { get; set; } = "";
        public string verification_code { get; set; } = "";
        public string username { get; set; } = "";
    }
}
