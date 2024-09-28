namespace Backend_Api_services.Models.DTOs
{
    public class ProfileRepsone
    {
        public int user_id { get; set; }
        public string profile_pic {  get; set; }
        public string fullname { get; set; }
        public string qr_code { get; set; }
        public double rating { get; set; }
        public string bio { get; set; }
        public int post_nb { get; set; }
        public int followers_nb { get; set; }
        public int following_nb { get; set; }
    }
}
