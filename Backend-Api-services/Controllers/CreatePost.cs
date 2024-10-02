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
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon;
using Amazon.Runtime;

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

            // Step 1: Create the Post entity and save it to the database
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

            _context.Posts.Add(newPost);
            await _context.SaveChangesAsync();

            // Step 2: Create a response DTO to return to the client
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

            // Step 3: Invoke Lambda to generate thumbnail using configuration settings
            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];
            var awsRegion = _configuration["AWS:Region"];

            var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
            var lambdaConfig = new AmazonLambdaConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion) };

            using (var lambdaClient = new AmazonLambdaClient(awsCredentials, lambdaConfig))
            {
                foreach (var media in newPost.Media)
                {
                    // Invoke Lambda function for each media item (if necessary)
                    var lambdaRequest = new InvokeRequest
                    {
                        FunctionName = "GenerateThumbnails",  // Replace with your Lambda function name
                        InvocationType = InvocationType.Event,    // Asynchronous invocation
                        Payload = $"{{ \"media_id\": \"{media.media_id}\", \"media_url\": \"{media.media_url}\" }}"  // Pass media_id and media_url
                    };

                    var response = await lambdaClient.InvokeAsync(lambdaRequest);
                    // Optional: Handle the response, log it if needed
                }
            }

            // Step 4: Return the created post details to the client
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
        [HttpPost("save-thumbnail-url")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveThumbnailUrl([FromBody] ThumbnailDto request)
        {
            if (request == null || request.MediaId == 0 || string.IsNullOrEmpty(request.ThumbnailUrl))
            {
                return BadRequest("Invalid data.");
            }

            // Find the PostMedia record by media_id
            var postMedia = await _context.PostMedias.FirstOrDefaultAsync(pm => pm.media_id == request.MediaId);
            if (postMedia == null)
            {
                return NotFound("Media not found.");
            }

            // Update the PostMedia record with the thumbnail URL
            postMedia.thumbnail_url = request.ThumbnailUrl;  // Assuming your entity has a 'thumbnail_url' column
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thumbnail URL saved successfully." });
        }

        // DTO for receiving thumbnail URL updates
        public class ThumbnailDto
        {
            public int MediaId { get; set; } // Ensure it's an integer
            public string? ThumbnailUrl { get; set; } // Nullable string for thumbnail URL
        }

    }
}
