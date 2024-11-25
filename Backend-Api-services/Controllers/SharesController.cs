using Backend_Api_services.Models.Entities;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // Ensure JWT authorization is applied to all endpoints
    public class SharesController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;
        private readonly INotificationService _notificationService;

        public SharesController(apiDbContext context, SignatureService signatureService, INotificationService notificationService)
        {
            _context = context;
            _signatureService = signatureService;
            _notificationService = notificationService;
        }

        // POST: api/Shares
        [HttpPost]
        public async Task<IActionResult> SharePost([FromBody] SharePostDto sharePostDto)
        {
            // Validate the signature using relevant fields
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{sharePostDto.UserId}:{sharePostDto.PostId}:{sharePostDto.Comment}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

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
                Comment = sharePostDto.Comment
            };
            _context.SharedPosts.Add(sharedPost);
            post.share_count++;
            await _context.SaveChangesAsync();

            // **Notification Logic Starts Here**

            var postOwnerId = post.user_id;
            var sharerId = sharePostDto.UserId;

            // Check if the sharer is not the post owner
            if (postOwnerId != sharerId)
            {
                // Retrieve the sharer's full name
                var sharer = await _context.users
                    .FirstOrDefaultAsync(u => u.user_id == sharerId);

                string sharerFullName = sharer?.fullname ?? "Someone";

                string message = $"{sharerFullName} shared your post.";

                // Prepare custom data if needed
                var data = new Dictionary<string, string>
        {
            { "type", "Share" },
            { "related_entity_id", post.post_id.ToString() },
            { "shared_post_id", sharedPost.PostId.ToString() } // Adjust if your shared_post entity has a different ID property
        };

                // Send and save the notification
                try
                {
                    await _notificationService.SendAndSaveNotificationAsync(
                        recipientUserId: postOwnerId,
                        senderUserId: sharerId,
                        type: "Share",
                        relatedEntityId: post.post_id,
                        message: message
                    );
                }
                catch (Exception ex)
                {
                    // Handle the exception as needed
                    // Optionally log or ignore
                }
            }

            // **Notification Logic Ends Here**

            return Ok("Post shared successfully.");
        }

        // GET: api/Shares/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSharedPost(int id)
        {
            // Extract and validate the signature for the GET request using the id
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{id}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

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
            // Extract and validate the signature for the GET request
            string signature = Request.Headers["X-Signature"];
            var dataToSign = "all";  // You could use something more meaningful if necessary

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

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
 