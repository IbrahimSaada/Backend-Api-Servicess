using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Backend_Api_services.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Backend_Api_services.Models.Entities;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;

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
        [HttpGet("userposts")]
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetUserPostsById(int userId, int viewerUserId, int pageNumber = 1, int pageSize = 10)
        {
            // Validate input parameters
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return BadRequest("Page number and page size must be greater than zero.");
            }

            // Fetch the profile information for the user being viewed
            var userProfile = await _context.users.FindAsync(userId);
            if (userProfile == null)
            {
                return NotFound("User profile not found.");
            }

            // Determine if the viewer is checking their own profile
            bool isOwner = (viewerUserId == userId);

            // Determine the visibility of posts based on profile privacy and follower approval
            bool isApprovedFollower = false;

            if (!isOwner && !userProfile.is_public)
            {
                // Check if the viewer is an approved follower for private profiles
                isApprovedFollower = await _context.Followers
                    .AnyAsync(f => f.followed_user_id == userId && f.follower_user_id == viewerUserId && f.approval_status == "approved");

                if (!isApprovedFollower)
                {
                    return StatusCode(403, "You are not allowed to view this user's private posts.");
                }
            }

            // Fetch the total number of posts for the user, considering visibility
            var totalPostsQuery = _context.Posts.Where(p => p.user_id == userId);

            if (!isOwner)
            {
                // If the viewer is not the owner, limit to public posts if not an approved follower
                if (!isApprovedFollower)
                {
                    totalPostsQuery = totalPostsQuery.Where(p => p.is_public);
                }
            }

            var totalPosts = await totalPostsQuery.CountAsync();

            // If no posts are found, return a message
            if (totalPosts == 0)
            {
                return StatusCode(204);  // No content, meaning there are no posts
            }

            // Fetch the posts with pagination
            var postsQuery = totalPostsQuery
                .Include(p => p.User)
                .Include(p => p.Media)
                .OrderByDescending(p => p.created_at)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            var posts = await postsQuery.ToListAsync();

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
                is_liked = _context.Likes.Any(like => like.post_id == post.post_id && like.user_id == viewerUserId),
                is_Bookmarked = _context.Bookmarks.Any(bookmark => bookmark.post_id == post.post_id && bookmark.user_id == viewerUserId)
            }).ToList();

            // Return the list of posts for the user
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
        // GET: api/UserProfile/{profileId}/follow-status
        [HttpGet("{profileId}/follow-status")]
        public ActionResult CheckFollowStatus(int profileId, int currentUserId)
        {
            // Fetch the profile user
            var profileUser = _context.users.FirstOrDefault(u => u.user_id == profileId);
            if (profileUser == null)
            {
                return NotFound("Profile user not found.");
            }

            // Check if the current user is following the profile user
            var isFollowing = _context.Followers.Any(f => f.follower_user_id == currentUserId && f.followed_user_id == profileId);

            // Check if the profile user is following the current user
            var amFollowing = _context.Followers.Any(f => f.follower_user_id == profileId && f.followed_user_id == currentUserId);

            // Create the response DTO
            var followStatusResponse = new FollowStatusResponse
            {
                IsFollowing = isFollowing,
                AmFollowing = amFollowing
            };

            // Return the follow status
            return Ok(followStatusResponse);
        }
        [HttpGet("sharedposts/{currentUserId}")]
        public async Task<IActionResult> GetSharedPostsForProfile(int currentUserId, int viewerUserId, int pageNumber = 1, int pageSize = 10)
        {
            // If the viewer (viewerUserId) is the same as the profile owner (currentUserId), show all posts
            if (viewerUserId == currentUserId)
            {
                // The profile owner is viewing their own profile
                return await GetSharedPostsForUser(currentUserId, pageNumber, pageSize);
            }

            // Retrieve the profile information of the profile owner (currentUserId)
            var userProfile = await _context.users.FindAsync(currentUserId);
            if (userProfile == null)
            {
                return NotFound("User profile not found.");
            }

            // If the profile is public, anyone can view the shared posts
            if (userProfile.is_public)
            {
                return await GetSharedPostsForUser(currentUserId, pageNumber, pageSize);
            }

            // If the profile is private, check if the viewer (viewerUserId) is an approved follower
            var follower = await _context.Followers
                .FirstOrDefaultAsync(f => f.followed_user_id == currentUserId && f.follower_user_id == viewerUserId && f.approval_status == "approved");

            if (follower == null)
            {
                // Viewer is not allowed to see the profile owner's shared posts
                return StatusCode(403, "You are not allowed to view this user's shared posts.");
            }

            // The viewer is an approved follower, so they can view the shared posts
            return await GetSharedPostsForUser(currentUserId, pageNumber, pageSize);
        }

        // Helper method to get shared posts for a specific user with pagination
        private async Task<IActionResult> GetSharedPostsForUser(int userId, int pageNumber, int pageSize)
        {
            var sharedPosts = await _context.SharedPosts
                .Where(sp => sp.SharerId == userId)  // Filter by the provided user ID
                .Include(sp => sp.Sharedby)           // Include the user who shared the post
                .Include(sp => sp.PostContent)        // Include the shared post content
                .ThenInclude(p => p.User)             // Ensure the User related to the Post is loaded
                .Include(sp => sp.PostContent.Media)  // Include the media associated with the post
                .Skip((pageNumber - 1) * pageSize)    // Skip the posts for previous pages
                .Take(pageSize)                       // Take the posts for the current page
                .ToListAsync();

            if (sharedPosts == null || !sharedPosts.Any())
            {
                return NotFound("No shared posts found for this user.");
            }

            var sharedPostDetailsDtos = sharedPosts.Select(sharedPost => new SharedPostDetailsDto
            {
                ShareId = sharedPost.ShareId,
                SharerId = sharedPost.SharerId,
                SharerUsername = sharedPost.Sharedby.fullname,
                SharerProfileUrl = sharedPost.Sharedby?.profile_pic,
                PostId = sharedPost.PostId,
                PostContent = sharedPost.PostContent.caption,
                PostCreatedAt = sharedPost.PostContent.created_at,
                Media = sharedPost.PostContent.Media.Select(pm => new PostMediaDto
                {
                    MediaUrl = pm.media_url,
                    MediaType = pm.media_type,
                    ThumbnailUrl = pm.thumbnail_url // Include thumbnail for video posts
                }).ToList(),
                SharedAt = sharedPost.SharedAt,
                Comment = sharedPost.Comment,
                OriginalPostUserUrl = sharedPost.PostContent?.User?.profile_pic,
                OriginalPostFullName = sharedPost.PostContent.User.fullname,
                OriginalPostUserId = sharedPost.PostContent.User.user_id,
                like_count = sharedPost.PostContent.like_count,
                comment_count = sharedPost.PostContent.comment_count,
                is_liked = _context.Likes.Any(like => like.post_id == sharedPost.PostId && like.user_id == userId),
                is_Bookmarked = _context.Bookmarks.Any(bookmark => bookmark.post_id == sharedPost.PostId && bookmark.user_id == userId)
            }).ToList();

            return Ok(sharedPostDetailsDtos);
        }

        [HttpGet("{userId}/followers/{viewerUserId}")]
        public async Task<ActionResult<IEnumerable<FollowerResponse>>> GetFollowers(int userId, int viewerUserId, string search = "", int pageNumber = 1, int pageSize = 10)
        {
            // Validate input parameters
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return BadRequest("Page number and page size must be greater than zero.");
            }

            // Validate the user exists
            var user = await _context.users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Check if the user allows public viewing of followers
            if (!user.isFollowersPublic && viewerUserId != userId)
            {
                return StatusCode(403, "The followers list is private.");
            }

            // Convert search term to lowercase for case-insensitive search
            search = search?.ToLower() ?? "";

            // Fetch the total number of followers for the user, filtered by the case-insensitive search term
            var totalFollowers = await _context.Followers
                .Where(f => f.followed_user_id == userId &&
                            (string.IsNullOrEmpty(search) || f.Follower.fullname.ToLower().Contains(search)))
                .CountAsync();

            // Fetch the followers with pagination and case-insensitive search
            var followers = await _context.Followers
                .Where(f => f.followed_user_id == userId &&
                            (string.IsNullOrEmpty(search) || f.Follower.fullname.ToLower().Contains(search)))
                .Select(f => new FollowerResponse
                {
                    FollowerId = f.follower_user_id,
                    FullName = f.Follower.fullname,  // Filtered by the search term
                    ProfilePic = f.Follower.profile_pic,
                })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Return the paginated followers with metadata
            var response = new
            {
                TotalFollowers = totalFollowers,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Followers = followers
            };

            return Ok(response);
        }


        [HttpGet("{userId}/following/{viewerUserId}")]
        public async Task<ActionResult<IEnumerable<FollowingResponse>>> GetFollowing(int userId, int viewerUserId, string search = "", int pageNumber = 1, int pageSize = 10)
        {
            // Validate input parameters
            if (pageNumber <= 0 || pageSize <= 0)
            {
                return BadRequest("Page number and page size must be greater than zero.");
            }

            // Validate the user exists
            var user = await _context.users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Check if the user allows public viewing of following list
            if (!user.isFollowingPublic && viewerUserId != userId)
            {
                return StatusCode(403, "The following list is private.");
            }

            // Convert search term to lowercase for case-insensitive search
            search = search?.ToLower() ?? "";

            // Fetch the total number of users that this user is following, filtered by the case-insensitive search term
            var totalFollowing = await _context.Followers
                .Where(f => f.follower_user_id == userId &&
                            (string.IsNullOrEmpty(search) || f.User.fullname.ToLower().Contains(search)))
                .CountAsync();

            // Fetch the following users with pagination and case-insensitive search
            var following = await _context.Followers
                .Where(f => f.follower_user_id == userId &&
                            (string.IsNullOrEmpty(search) || f.User.fullname.ToLower().Contains(search)))
                .Select(f => new FollowingResponse
                {
                    FollowedUserId = f.followed_user_id,
                    FullName = f.User.fullname,  // Filtered by the search term
                    ProfilePic = f.User.profile_pic,
                })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Return the paginated following list with metadata
            var response = new
            {
                TotalFollowing = totalFollowing,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Following = following
            };

            return Ok(response);
        }
        [HttpPut("change-privacy")]
        public async Task<IActionResult> ChangeAllPrivacySettings(int userId, bool? isPublic = null, bool? isFollowersPublic = null, bool? isFollowingPublic = null)
        {
            // Fetch the user profile by userId
            var userProfile = await _context.users.FindAsync(userId);
            if (userProfile == null)
            {
                return NotFound("User profile not found.");
            }

            // Update the user's privacy settings only if the corresponding parameters are provided
            if (isPublic.HasValue)
            {
                userProfile.is_public = isPublic.Value;
            }

            if (isFollowersPublic.HasValue)
            {
                userProfile.isFollowersPublic = isFollowersPublic.Value;
            }

            if (isFollowingPublic.HasValue)
            {
                userProfile.isFollowingPublic = isFollowingPublic.Value;
            }

            // Save changes to the database
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Privacy settings updated successfully." });
            }
            catch (Exception ex)
            {
                // Log or handle the error
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("check-privacy/{userId}")]
        public async Task<IActionResult> CheckProfilePrivacy(int userId)
        {
            // Fetch the user profile by userId
            var userProfile = await _context.users.FindAsync(userId);
            if (userProfile == null)
            {
                return NotFound("User profile not found.");
            }

            // Return the privacy status for the profile
            return Ok(new
            {
                isPublic = userProfile.is_public,
                isFollowersPublic = userProfile.isFollowersPublic,  // Include followers privacy
                isFollowingPublic = userProfile.isFollowingPublic   // Include following privacy
            });
        }

        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequestDto changePasswordDto)
        {
            // Fetch the user by ID
            var user = await _context.users.FirstOrDefaultAsync(u => u.user_id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Verify the old password (plain text comparison)
            if (changePasswordDto.OldPassword != user.password)
            {
                // Return specific error for incorrect old password
                return BadRequest(new { error = "Old password is incorrect." });
            }

            // Validate the new password using the IsValidPassword method
            if (!IsValidPassword(changePasswordDto.NewPassword))
            {
                // Return specific error for invalid new password complexity
                return BadRequest(new { error = "New password must be at least 8 characters long and include a mix of uppercase, lowercase, numbers, and special characters." });
            }

            // Update the user's password with the new password
            user.password = changePasswordDto.NewPassword;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changed successfully." });
        }

        // Helper method to validate password complexity
        private bool IsValidPassword(string password)
        {
            var passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";
            return Regex.IsMatch(password, passwordPattern);
        }

        [HttpDelete("delete-post/{postId}")]
        public async Task<IActionResult> DeletePost(int postId, int userId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.post_id == postId && p.user_id == userId);
            if (post == null)
            {
                return NotFound("Post not found or you do not have permission to delete this post.");
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok("Post deleted successfully.");
        }

        [HttpPut("edit-post/{postId}")]
        public async Task<IActionResult> EditPostCaption(int postId, [FromBody] string newCaption, int userId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.post_id == postId && p.user_id == userId);
            if (post == null)
            {
                return NotFound("Post not found or you do not have permission to edit this post.");
            }

            post.caption = newCaption;
            await _context.SaveChangesAsync();

            return Ok("Post caption updated successfully.");
        }

        [HttpDelete("delete-shared-post/{sharedPostId}")]
        public async Task<IActionResult> DeleteSharedPost(int sharedPostId, int userId)
        {
            var sharedPost = await _context.SharedPosts.FirstOrDefaultAsync(sp => sp.ShareId == sharedPostId && sp.SharerId == userId);
            if (sharedPost == null)
            {
                return NotFound("Shared post not found or you do not have permission to delete this shared post.");
            }

            _context.SharedPosts.Remove(sharedPost);
            await _context.SaveChangesAsync();

            return Ok("Shared post deleted successfully.");
        }

        [HttpPut("edit-shared-post/{sharedPostId}")]
        public async Task<IActionResult> EditSharedPostComment(int sharedPostId, [FromBody] string newComment, int userId)
        {
            var sharedPost = await _context.SharedPosts.FirstOrDefaultAsync(sp => sp.ShareId == sharedPostId && sp.SharerId == userId);
            if (sharedPost == null)
            {
                return NotFound("Shared post not found or you do not have permission to edit this shared post.");
            }

            sharedPost.Comment = newComment;
            await _context.SaveChangesAsync();

            return Ok("Shared post comment updated successfully.");
        }

    }

}

