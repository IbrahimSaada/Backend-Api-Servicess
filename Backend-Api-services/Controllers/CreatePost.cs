using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CreatePostController : ControllerBase
    {
        private readonly apiDbContext _context;

        public CreatePostController(apiDbContext context)
        {
            _context = context;
        }

        // POST: api/createpost
        [HttpPost]
        public async Task<ActionResult<PostResponse>> CreatePost([FromBody] PostRequest postRequest)
        {
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
    }
}
