namespace Backend_Api_services.Models.DTOs
{
public class FollowerResponse
{
    public int FollowerId { get; set; }
    public string FullName { get; set; }
    public string ProfilePic { get; set; }
}

public class FollowingResponse
{
    public int FollowedUserId { get; set; }
    public string FullName { get; set; }
    public string ProfilePic { get; set; }
}

}
