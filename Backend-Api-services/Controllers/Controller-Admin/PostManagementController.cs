using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin,superadmin")]  // Ensure only admins or superadmins can access this controller
    public class PostManagementController : ControllerBase
    {
        private readonly apiDbContext _context;

        public PostManagementController(apiDbContext context)
        {
            _context = context;
        }

        // GET: api/PostManagement/Count
        [HttpGet("count")]
        public async Task<IActionResult> GetPostCount()
        {
            // Get the total number of posts in the system
            var postCount = await _context.Posts.CountAsync();

            return Ok(new { TotalPosts = postCount });
        }
        // GET: api/Posts/display
        [HttpGet("display")]
        [AllowAnonymous] // Allows anyone to access this endpoint without authentication
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts()
        {
            // Fetch all public posts from the database
            var posts = await _context.Posts
                                      .Include(p => p.User)
                                      .Include(p => p.Media)
                                      .Where(p => p.is_public) // Only public posts
                                      .OrderByDescending(p => p.created_at)
                                      .ToListAsync();

            // Convert posts to PostResponse DTO format
            var postResponses = posts.Select(post => new PostResponse
            {
                post_id = post.post_id,
                caption = post.caption,
                comment_count = post.comment_count,
                created_at = post.created_at,
                is_public = post.is_public,
                like_count = post.like_count,
                user_id = post.user_id,
                fullname = post.User.fullname,
                profile_pic = post.User.profile_pic,
                Media = post.Media.Select(media => new PostMediaResponse
                {
                    media_id = media.media_id,
                    media_url = media.media_url,
                    media_type = media.media_type,
                    post_id = media.post_id
                }).ToList()
            }).ToList();

            // Return the list of posts
            return Ok(postResponses);
        }
    }
}
