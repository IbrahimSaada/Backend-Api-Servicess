// In PostsController.cs

using Backend_Api_services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestSharp;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        var posts = await _context.posts
                                  .Join(_context.users,
                                        post => post.user_id,
                                        user => user.user_id,
                                        (post, user) => new PostResponse
                                        {
                                            post_id = post.post_id,
                                            caption = post.caption,
                                            comment_count = post.comment_count,
                                            created_at = post.created_at,
                                            is_public = post.is_public,
                                            like_count = post.like_count,
                                            media_type = post.media_type,
                                            media_url = post.media_url,
                                            user_id = post.user_id,
                                            fullname = user.fullname,
                                            profile_pic = user.profile_pic
                                        })
                                  .Where(p => p.is_public)
                                  .OrderByDescending(p => p.created_at)
                                  .ToListAsync();

        return Ok(posts);
    }
}


