using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
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
    public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts()
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
            }).ToList()
        }).ToList();

        return Ok(postResponses);
    }
}
