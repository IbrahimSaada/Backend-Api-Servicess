using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private readonly apiDbContext _context;

        public UserProfileController(apiDbContext context)
        {
            _context = context;
        }

        // GET: api/UserProfile/{id}
        [HttpGet("{id}")]
        public ActionResult GetUserProfileById(int id)
        {
            // Fetch user data from Users table
            var user = _context.users.FirstOrDefault(u => u.user_id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Fetch number of followers and following count
            var followersCount = _context.Followers.Count(f => f.followed_user_id == id);
            var followingCount = _context.Followers.Count(f => f.follower_user_id == id);
            // Fetch post count
             var postCount = _context.Posts.Count(p => p.user_id == id);

            // Create ProfileResponse DTO
            var profileResponse = new ProfileRepsone
            {
                user_id = user.user_id,
                profile_pic = user.profile_pic,
                fullname = user.fullname,
                qr_code = user.qr_code,
                rating = user.rating,
                bio = user.bio,
                post_nb = postCount,
                followers_nb = followersCount,
                following_nb = followingCount
            };

            // Return the response
            return Ok(profileResponse);
        }
        // POST: api/UserProfile/{id}/edit
        [HttpPost("{id}/edit")]
        public ActionResult UpdateUserProfile(int id, [FromBody] ProfileUpdateRequestDto profileUpdate)
        {
            // Fetch the user by ID
            var user = _context.users.FirstOrDefault(u => u.user_id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Update the profile fields if they are provided in the request
            if (!string.IsNullOrEmpty(profileUpdate.profile_pic))
            {
                user.profile_pic = profileUpdate.profile_pic;
            }

            if (!string.IsNullOrEmpty(profileUpdate.fullname))
            {
                user.fullname = profileUpdate.fullname;
            }

            if (!string.IsNullOrEmpty(profileUpdate.bio))
            {
                user.bio = profileUpdate.bio;
            }

            // Save changes to the database
            _context.SaveChanges();

            return Ok("Profile updated successfully.");
        }
    }
}

