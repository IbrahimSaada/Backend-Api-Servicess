using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Authorization;  // For JWT Authorization
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;  // For HMAC
using System.Text;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]  // Require JWT authentication for the whole controller
    public class CreatePostController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly IConfiguration _configuration;

        public CreatePostController(apiDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/createpost
        [HttpPost]
        public async Task<ActionResult<PostResponse>> CreatePost([FromBody] PostRequest postRequest)
        {
            // Check if the signature is valid
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature) || !ValidateSignature(signature, postRequest))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            if (postRequest == null)
            {
                return BadRequest("Invalid post data.");
            }

            // Create the Post entity
            var newPost = new Post
            {
                user_id = postRequest.user_id,
                caption = postRequest.caption,
                created_at = DateTime.UtcNow,
                is_public = postRequest.is_public,
                Media = postRequest.Media.Select(m => new PostMedia
                {
                    media_url = m.media_url,
                    media_type = m.media_type
                }).ToList()
            };

            // Add and save the new Post entity
            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync();

            // Create a response DTO
            var postResponse = new PostResponse
            {
                post_id = newPost.post_id,
                user_id = newPost.user_id,
                caption = newPost.caption,
                created_at = newPost.created_at,
                is_public = newPost.is_public,
                like_count = newPost.like_count,
                comment_count = newPost.comment_count,
                Media = newPost.Media.Select(m => new PostMediaResponse
                {
                    media_id = m.media_id,
                    media_url = m.media_url,
                    media_type = m.media_type,
                    post_id = m.post_id
                }).ToList()
            };

            // Return the created Post as a response
            return CreatedAtAction(nameof(GetPost), new { id = newPost.post_id }, postResponse);
        }

        // GET: api/createpost/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<PostResponse>> GetPost(int id)
        {
            var post = await _context.Posts.Include(p => p.Media)
                                           .FirstOrDefaultAsync(p => p.post_id == id);

            if (post == null)
            {
                return NotFound();
            }

            var postResponse = new PostResponse
            {
                post_id = post.post_id,
                user_id = post.user_id,
                caption = post.caption,
                created_at = post.created_at,
                is_public = post.is_public,
                like_count = post.like_count,
                comment_count = post.comment_count,
                Media = post.Media.Select(m => new PostMediaResponse
                {
                    media_id = m.media_id,
                    media_url = m.media_url,
                    media_type = m.media_type,
                    post_id = m.post_id
                }).ToList()
            };

            return Ok(postResponse);
        }

        // Helper method to validate the HMAC signature
        private bool ValidateSignature(string receivedSignature, PostRequest postRequest)
        {
            // Concatenate the data to sign
            var dataToSign = $"{postRequest.user_id}:{postRequest.caption}:{postRequest.is_public.ToString().ToLower()}";
            Console.WriteLine($"Data Signed on Server: {dataToSign}");  // Log the exact data being signed

            // Retrieve the shared secret key
            var secretKey = _configuration["AppSecretKey"];

            // Compute the HMAC-SHA256 signature
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
                var computedSignature = Convert.ToBase64String(computedHash);


                // Debugging: Log the computed signature and the received signature
                Console.WriteLine($"Computed Signature: {computedSignature}");
                Console.WriteLine($"Received Signature: {receivedSignature}");

                // Compare the computed signature with the received one
                return computedSignature == receivedSignature;
            }
        }
    }
}
