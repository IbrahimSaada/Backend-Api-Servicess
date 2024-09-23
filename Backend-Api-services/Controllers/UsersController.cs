using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data; // assuming this is where apiDbContext is located
using Backend_Api_services.Models.DTOs; // assuming the DTO is in this namespace
using Backend_Api_services.Models.Entities; // assuming the entity is in this namespace
using System.Linq;


    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly apiDbContext _context;

        public UsersController(apiDbContext context)
        {
            _context = context;
        }

    // GET: api/Users/search?fullname=searchTerm&currentUserId=1&pageNumber=1&pageSize=10
    [HttpGet("search")]
    public ActionResult<List<UserDto>> SearchUsersByFullname(string fullname, int currentUserId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.users
            .Where(u => u.fullname.ToLower().Contains(fullname.ToLower()) && u.user_id != currentUserId) // Exclude current user from search
            .OrderBy(u => u.fullname); // You can modify the sorting logic if needed

        // Calculate total count of users that match the search
        var totalUsers = query.Count();

        // Implement pagination
        var users = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto
            {
                user_id = u.user_id,
                fullname = u.fullname,
                username = u.username,
                profile_pic = u.profile_pic,
                bio = u.bio,
                phone_number = u.phone_number,
                // Check if the searched user is following the current user
                is_following = _context.Followers.Any(f => f.user_id == u.user_id && f.follower_user_id == currentUserId),
                // Check if the current user is following the searched user
                am_following = _context.Followers.Any(f => f.user_id == currentUserId && f.follower_user_id == u.user_id)
            })
            .ToList();

        // Return results and pagination metadata
        var result = new
        {
            TotalUsers = totalUsers,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Users = users
        };

        return Ok(result);
    }

    // POST: api/Users/follow
    [HttpPost("follow")]
    public ActionResult FollowUser(FollowUserDto followUserDto)
    {
        // Check if both users exist in the database
        var user = _context.users.FirstOrDefault(u => u.user_id == followUserDto.user_id);
        var follower = _context.users.FirstOrDefault(u => u.user_id == followUserDto.follower_user_id);

        if (user == null || follower == null)
        {
            return BadRequest("User or follower not found.");
        }

        // Check if the user is already following the other user
        var existingFollow = _context.Followers
            .FirstOrDefault(f => f.user_id == followUserDto.user_id && f.follower_user_id == followUserDto.follower_user_id);

        if (existingFollow != null)
        {
            return BadRequest("You are already following this user.");
        }

        // Create the follow record
        var follow = new Followers
        {
            user_id = followUserDto.user_id,
            follower_user_id = followUserDto.follower_user_id,
            is_public = followUserDto.is_public
        };

        _context.Followers.Add(follow);
        _context.SaveChanges();

        return Ok("User followed successfully.");
    }

    // DELETE: api/Users/unfollow
    [HttpDelete("unfollow")]
    public ActionResult UnfollowUser(FollowUserDto followUserDto)
    {
        // Check if the following relationship exists
        var followRecord = _context.Followers
            .FirstOrDefault(f => f.user_id == followUserDto.user_id && f.follower_user_id == followUserDto.follower_user_id);

        if (followRecord == null)
        {
            return NotFound("You are not following this user.");
        }

        // Remove the follow record
        _context.Followers.Remove(followRecord);
        _context.SaveChanges();

        return Ok("Unfollowed successfully.");
    }
}

