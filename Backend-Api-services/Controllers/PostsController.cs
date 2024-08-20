using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class PostsController : ControllerBase
{
    private readonly apiDbContext _context;

    public PostsController(apiDbContext context)
    {
        _context = context;
    }

    // GET: api/Posts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts(int userId)
    {
        var posts = await _context.Posts
                                  .Include(p => p.User)
                                  .Include(p => p.Media) // Include related media
                                  .Where(p => p.is_public)
                                  .OrderByDescending(p => p.created_at)
                                  .ToListAsync();

        // Map the entity data to DTOs
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
            }).ToList(),
            is_liked = _context.Likes.Any(like => like.post_id == post.post_id && like.user_id == userId)
        }).ToList();

        return Ok(postResponses);
    }
    // POST: api/Posts/Like
    [HttpPost("Like")]
    public async Task<IActionResult> LikePost([FromBody] LikeRequest likeRequest)
    {
        var postId = likeRequest.post_id;
        var userId = likeRequest.user_id;

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var existingLike = await _context.Likes
            .FirstOrDefaultAsync(l => l.post_id == postId && l.user_id == userId);

        if (existingLike != null)
        {
            return BadRequest("You have already liked this post.");
        }

        var like = new Like
        {
            post_id = postId,
            user_id = userId
        };

        _context.Likes.Add(like);

        // Increment the like_count in the Posts table
        post.like_count += 1;

        await _context.SaveChangesAsync();

        return Ok("Post liked successfully.");
    }

    // POST: api/Posts/Unlike
    [HttpPost("Unlike")]
    public async Task<IActionResult> UnlikePost([FromBody] LikeRequest likeRequest)
    {
        var postId = likeRequest.post_id;
        var userId = likeRequest.user_id;

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var like = await _context.Likes
            .FirstOrDefaultAsync(l => l.post_id == postId && l.user_id == userId);

        if (like == null)
        {
            return BadRequest("You have not liked this post.");
        }

        // Remove the like record
        _context.Likes.Remove(like);

        // Decrement the like_count in the Posts table
        post.like_count -= 1;

        await _context.SaveChangesAsync();

        return Ok("Like removed successfully.");
    }


}
