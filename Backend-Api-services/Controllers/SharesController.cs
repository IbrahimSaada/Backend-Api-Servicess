using Backend_Api_services.Models.Entities;
using Backend_Api_services.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SharesController : ControllerBase
    {
        private readonly apiDbContext _context;

        public SharesController(apiDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SharePost([FromBody] SharePostDto sharePostDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if the user and post exist
            var user = await _context.users.FindAsync(sharePostDto.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var post = await _context.Posts.FindAsync(sharePostDto.PostId);
            if (post == null)
            {
                return NotFound("Post not found.");
            }

            // Create the SharedPost entity
            var sharedPost = new shared_posts
            {
                SharerId = sharePostDto.UserId,
                PostId = sharePostDto.PostId,
                SharedAt = DateTime.UtcNow,
                Comment = sharePostDto.Comment // Handle the comment
            };

            // Add the new entry to the database
            _context.SharedPosts.Add(sharedPost);
            await _context.SaveChangesAsync();

            return Ok("Post shared successfully.");
        }

        // GET: api/Shares/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSharedPost(int id)
        {
            var sharedPost = await _context.SharedPosts
                .Include(sp => sp.Sharedby)     // Include the user who shared the post
                .Include(sp => sp.PostContent)  // Include the shared post content
                .ThenInclude(p => p.User)       // Ensure the User related to the Post is loaded
                .Include(sp => sp.PostContent.Media)  // Include the media associated with the post
                .FirstOrDefaultAsync(sp => sp.ShareId == id);

            if (sharedPost == null)
            {
                return NotFound("Shared post not found.");
            }

            // Get the original post user's profile URL (can be null)
            var originalPostUserUrl = sharedPost.PostContent?.User?.profile_pic;

            // Get the sharer's profile URL (can be null)
            var sharerProfileUrl = sharedPost.Sharedby?.profile_pic;

            // Map the SharedPost entity to SharedPostDetailsDto
            var sharedPostDetailsDto = new SharedPostDetailsDto
            {
                ShareId = sharedPost.ShareId,
                SharerId = sharedPost.SharerId,
                SharerUsername = sharedPost.Sharedby.fullname,
                SharerProfileUrl = sharerProfileUrl,  // Use the profile URL, which can be null
                PostId = sharedPost.PostId,
                PostContent = sharedPost.PostContent.caption,
                PostCreatedAt = sharedPost.PostContent.created_at,
                Media = sharedPost.PostContent.Media.Select(pm => new PostMediaDto
                {
                    MediaUrl = pm.media_url,
                    MediaType = pm.media_type
                }).ToList(),
                SharedAt = sharedPost.SharedAt,
                Comment = sharedPost.Comment,
                OriginalPostUserUrl = originalPostUserUrl  // Use the profile URL, which can be null
            };

            return Ok(sharedPostDetailsDto);
        }

        // GET: api/Shares
        [HttpGet]
        public async Task<IActionResult> GetAllSharedPosts()
        {
            var sharedPosts = await _context.SharedPosts
                .Include(sp => sp.Sharedby)     // Include the user who shared the post
                .Include(sp => sp.PostContent)  // Include the shared post content
                .ThenInclude(p => p.User)       // Ensure the User related to the Post is loaded
                .Include(sp => sp.PostContent.Media)  // Include the media associated with the post
                .ToListAsync();

            var sharedPostDetailsDtos = sharedPosts.Select(sharedPost => new SharedPostDetailsDto
            {
                ShareId = sharedPost.ShareId,
                SharerId = sharedPost.SharerId,
                SharerUsername = sharedPost.Sharedby.fullname,
                SharerProfileUrl = sharedPost.Sharedby?.profile_pic,  // Handle nullable profile_pic
                PostId = sharedPost.PostId,
                PostContent = sharedPost.PostContent.caption,
                PostCreatedAt = sharedPost.PostContent.created_at,
                Media = sharedPost.PostContent.Media.Select(pm => new PostMediaDto
                {
                    MediaUrl = pm.media_url,
                    MediaType = pm.media_type
                }).ToList(),
                SharedAt = sharedPost.SharedAt,
                Comment = sharedPost.Comment,
                OriginalPostUserUrl = sharedPost.PostContent?.User?.profile_pic  // Handle nullable profile_pic
            }).ToList();

            return Ok(sharedPostDetailsDtos);
        }
    }
}
