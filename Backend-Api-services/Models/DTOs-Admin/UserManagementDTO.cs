namespace Backend_Api_services.Models.Entites_Admin
{
    public class UserManagementDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string ProfilePic { get; set; }
        public string Bio { get; set; }
        public int Rating { get; set; }
        public string PhoneNumber { get; set; }

        private DateTime? _verifiedAt;

        public DateTime? VerifiedAt
        {
            get => _verifiedAt;
            set => _verifiedAt = value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value;
        }

        public DateTime Dob { get; set; }
        public string Gender { get; set; }
        public string Fullname { get; set; }
    }
}
