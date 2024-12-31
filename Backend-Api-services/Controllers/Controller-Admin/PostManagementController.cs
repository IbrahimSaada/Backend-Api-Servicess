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
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Ensure valid pagination parameters
            if (page < 1 || pageSize < 1)
            {
                return BadRequest("Page and pageSize must be greater than 0.");
            }

            // Calculate the number of items to skip
            var skip = (page - 1) * pageSize;

            // Fetch total count for pagination metadata
            var totalPosts = await _context.Posts.Where(p => p.is_public).CountAsync();

            // Fetch paginated public posts from the database
            var posts = await _context.Posts
                                      .Include(p => p.User)
                                      .Include(p => p.Media)
                                      .Where(p => p.is_public) // Only public posts
                                      .OrderByDescending(p => p.created_at)
                                      .Skip(skip)
                                      .Take(pageSize)
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

            // Add pagination metadata
            var metadata = new
            {
                totalCount = totalPosts,
                pageSize,
                currentPage = page,
                totalPages = (int)Math.Ceiling(totalPosts / (double)pageSize)
            };

            // Return the list of posts with metadata
            return Ok(new { metadata, data = postResponses });
        }
        //delete post

        [HttpDelete("delete-post/{postId}")]
        public async Task<IActionResult> DeletePost(int postId)
        {
            // Fetch the post by postId
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.post_id == postId);
            if (post == null)
            {
                return NotFound("Post not found.");
            }

            // Remove the post
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok("Post deleted successfully.");
        }
    }
}
