using System.ComponentModel.DataAnnotations;

public class Users
{
    [Key]
    public int user_id { get; set; }

    public string username { get; set; } = "";

    private string _email = "";

    [Required]
    [EmailAddress]
    public string email
    {
        get => _email;
        set => _email = value.ToLower();
    }

    [Required]
    public string password { get; set; } = "";

    public string profile_pic { get; set; } = "";

    public string bio { get; set; } = "";

    public string qr_code { get; set; } = "";

    public double rating { get; set; } = 0;

    [Required]
    [Phone]
    public string phone_number { get; set; } = "";

    public string? verification_code { get; set; } = "";

    public DateTime? verified_at { get; set; } = null;

    public DateTime dob { get; set; } = DateTime.MinValue;

    public string gender { get; set; } = "";

    [Required]
    public string fullname { get; set; } = "";
}
