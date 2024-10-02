using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private readonly apiDbContext _context;

        public UserProfileController(apiDbContext context)
        {
            _context = context;
        }

        // GET: api/UserProfile/{id}
        [HttpGet("{id}")]
        public ActionResult GetUserProfileById(int id)
        {
            // Fetch user data from Users table
            var user = _context.users.FirstOrDefault(u => u.user_id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Fetch number of followers and following count
            var followersCount = _context.Followers.Count(f => f.followed_user_id == id);
            var followingCount = _context.Followers.Count(f => f.follower_user_id == id);
            // Fetch post count
             var postCount = _context.Posts.Count(p => p.user_id == id);

            // Create ProfileResponse DTO
            var profileResponse = new ProfileRepsone
            {
                user_id = user.user_id,
                profile_pic = user.profile_pic,
                fullname = user.fullname,
                qr_code = user.qr_code,
                rating = user.rating,
                bio = user.bio,
                post_nb = postCount,
                followers_nb = followersCount,
                following_nb = followingCount
            };

            // Return the response
            return Ok(profileResponse);
        }
        // POST: api/UserProfile/{id}/edit
        [HttpPost("{id}/edit")]
        public ActionResult UpdateUserProfile(int id, [FromBody] ProfileUpdateRequestDto profileUpdate)
        {
            // Fetch the user by ID
            var user = _context.users.FirstOrDefault(u => u.user_id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Update the profile fields if they are provided in the request
            if (!string.IsNullOrEmpty(profileUpdate.profile_pic))
            {
                user.profile_pic = profileUpdate.profile_pic;
            }

            if (!string.IsNullOrEmpty(profileUpdate.fullname))
            {
                user.fullname = profileUpdate.fullname;
            }

            if (!string.IsNullOrEmpty(profileUpdate.bio))
            {
                user.bio = profileUpdate.bio;
            }

            // Save changes to the database
            _context.SaveChanges();

            return Ok("Profile updated successfully.");
        }
        // GET: api/Posts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetUserPostsById(int userId, int pageNumber = 1, int pageSize = 10)
        {
            // Validate input parameters
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return BadRequest("Page number and page size must be greater than zero.");
            }

            // Fetch the total number of posts for the user
            var totalPosts = await _context.Posts
                                           .Where(p => p.user_id == userId && p.is_public)
                                           .CountAsync();

            // Fetch posts that belong to the user with userId and are public, with pagination
            var posts = await _context.Posts
                                      .Include(p => p.User)
                                      .Include(p => p.Media)
                                      .Where(p => p.user_id == userId && p.is_public)
                                      .OrderByDescending(p => p.created_at)
                                      .Skip((pageNumber - 1) * pageSize)  // Skip previous pages
                                      .Take(pageSize)  // Take the current page size
                                      .ToListAsync();

            // Map the posts to the PostResponse DTO
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
                    post_id = media.post_id,
                    thumbnail_url = media.thumbnail_url
                }).ToList(),
                is_liked = _context.Likes.Any(like => like.post_id == post.post_id && like.user_id == userId),
                is_Bookmarked = _context.Bookmarks.Any(bookmark => bookmark.post_id == post.post_id && bookmark.user_id == userId)
            }).ToList();


            // Return the list of posts that belong to the user
            return Ok(postResponses);
        }
        [HttpGet("bookmarked")]
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetBookmarkedPostsByUserId(int userId, int pageNumber = 1, int pageSize = 10)
        {
            // Validate input parameters
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return BadRequest("Page number and page size must be greater than zero.");
            }

            // Fetch the total number of bookmarked posts for the user
            var totalBookmarkedPosts = await _context.Bookmarks
                                                     .Where(b => b.user_id == userId)
                                                     .CountAsync();

            // Fetch the bookmarked posts for the user, with pagination
            var bookmarks = await _context.Bookmarks
                                          .Where(b => b.user_id == userId)
                                          .Include(b => b.post)  // Include the Post data
                                          .ThenInclude(p => p.User)  // Include the User data
                                          .Include(b => b.post.Media)  // Include the Media data
                                          .OrderByDescending(b => b.post.created_at)
                                          .Skip((pageNumber - 1) * pageSize)
                                          .Take(pageSize)
                                          .ToListAsync();

            // Map the posts to the PostResponse DTO
            var postResponses = bookmarks.Select(bookmark => new PostResponse
            {
                post_id = bookmark.post.post_id,
                caption = bookmark.post.caption,
                comment_count = bookmark.post.comment_count,
                created_at = bookmark.post.created_at,
                is_public = bookmark.post.is_public,
                like_count = bookmark.post.like_count,
                user_id = bookmark.post.user_id,
                fullname = bookmark.post.User.fullname,
                profile_pic = bookmark.post.User.profile_pic,
                Media = bookmark.post.Media.Select(media => new PostMediaResponse
                {
                    media_id = media.media_id,
                    media_url = media.media_url,
                    media_type = media.media_type,
                    post_id = media.post_id,
                    thumbnail_url = media.thumbnail_url
                }).ToList(),
                is_liked = _context.Likes.Any(like => like.post_id == bookmark.post.post_id && like.user_id == userId),
                is_Bookmarked = true  // Since these are bookmarked posts, this is always true
            }).ToList();

            return Ok(postResponses);
        }

    }
}

