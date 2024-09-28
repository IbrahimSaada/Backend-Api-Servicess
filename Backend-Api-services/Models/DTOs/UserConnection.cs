using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Backend_Api_services.Models.DTOs
{
    public class UserConnection
    {
        public int user_id { get; set; }
        public string fullname { get; set; } = "";
        public string username { get; set; } = "";
        public string profile_pic { get; set; } = "";
        public string bio { get; set; } = "";
        public string phone_number { get; set; } = "";
        public bool is_following { get; set; }
        public bool am_following { get; set; }
        public bool is_dismissed { get; set; }
    }
}
