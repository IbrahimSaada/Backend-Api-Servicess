using Microsoft.AspNetCore.Mvc;
using Backend_Api_services.Models.Data; // assuming this is where apiDbContext is located
using Backend_Api_services.Models.DTOs; // assuming the DTO is in this namespace
using Backend_Api_services.Models.Entities; // assuming the entity is in this namespace
using Backend_Api_services.Services;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Backend_Api_services.Services.Interfaces;
using Microsoft.EntityFrameworkCore;


    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserConnectionsController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;
        private readonly INotificationService _notificationService;

    public UserConnectionsController(apiDbContext context, SignatureService signatureService, INotificationService notificationService)
        {
        _context = context;
        _signatureService = signatureService;
        _notificationService = notificationService;
    }

    // GET: api/Users/search?fullname=searchTerm&currentUserId=1&pageNumber=1&pageSize=10
    [HttpGet("search")]
    public ActionResult SearchUsersByFullname(string fullname, int currentUserId, int pageNumber = 1, int pageSize = 10)
    {
        // Extract and validate the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{fullname}:{currentUserId}:{pageNumber}:{pageSize}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Input validation to prevent SQL injection or unwanted behaviors
        if (string.IsNullOrWhiteSpace(fullname) || pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest("Invalid search parameters.");
        }

        // Convert fullname to lowercase once for efficiency
        var searchFullName = fullname.ToLower();

        // Execute the query
        var query = _context.users
            .Where(u => u.fullname.ToLower().Contains(searchFullName) && u.user_id != currentUserId)
            .OrderBy(u => u.fullname);

        // Calculate total count of users that match the search
        var totalUsers = query.Count();

        // Implement pagination efficiently
        var users = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserConnection
            {
                user_id = u.user_id,
                fullname = u.fullname,
                username = u.username,
                profile_pic = u.profile_pic,
                bio = u.bio,
                phone_number = u.phone_number,
                is_following = _context.Followers.Any(f => f.followed_user_id == currentUserId && f.follower_user_id == u.user_id),
                am_following = _context.Followers.Any(f => f.followed_user_id == u.user_id && f.follower_user_id == currentUserId)
            })
            .ToList();

        // Return results with pagination metadata
        var result = new
        {
            TotalUsers = totalUsers,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Users = users
        };

        return Ok(result);
    }


    // POST: api/Users/follow
    [HttpPost("follow")]
    [AllowAnonymous]
    public async Task<ActionResult> FollowUser([FromBody] FollowUserDto followUserDto)
    {
        /*
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{followUserDto.follower_user_id}:{followUserDto.followed_user_id}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }
        */

        // Check if both users exist in the database
        var followedUser = _context.users.FirstOrDefault(u => u.user_id == followUserDto.followed_user_id);
        var followerUser = _context.users.FirstOrDefault(u => u.user_id == followUserDto.follower_user_id);

        if (followedUser == null || followerUser == null)
        {
            return BadRequest("User or follower not found.");
        }

        // Check if the user is already following the other user
        var existingFollow = _context.Followers
            .FirstOrDefault(f => f.followed_user_id == followUserDto.followed_user_id && f.follower_user_id == followUserDto.follower_user_id);

        if (existingFollow != null)
        {
            return BadRequest("You are already following this user.");
        }

        // Determine approval status based on the is_public flag
        var approvalStatus = followedUser.is_public ? "approved" : "pending";

        // Create the follow record
        var follow = new Followers
        {
            followed_user_id = followUserDto.followed_user_id,
            follower_user_id = followUserDto.follower_user_id,
            approval_status = approvalStatus
        };

        _context.Followers.Add(follow);
        _context.SaveChanges();

        // **Notification Logic Delegated to the Service**

        // Check if the followed user is already following back
        var isFollowedUserFollowingBack = _context.Followers.Any(f =>
            f.followed_user_id == followUserDto.follower_user_id &&
            f.follower_user_id == followUserDto.followed_user_id &&
            f.approval_status == "approved");

        // Avoid sending notification to self
        if (followedUser.user_id != followerUser.user_id)
        {
            try
            {
                await _notificationService.HandleFollowNotificationAsync(
                    recipientUserId: followedUser.user_id,
                    senderUserId: followerUser.user_id,
                    isMutualFollow: isFollowedUserFollowingBack
                );
            }
            catch (Exception ex)
            {
                // Handle the exception as needed (log or ignore)
            }
        }

        return Ok($"Follow request {approvalStatus} successfully.");
    }


    // DELETE: api/Users/unfollow
    [HttpDelete("unfollow")]
    public ActionResult UnfollowUser([FromBody] FollowUserDto followUserDto)
    {
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{followUserDto.follower_user_id}:{followUserDto.followed_user_id}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Input validation to ensure IDs are valid
        if (followUserDto.follower_user_id <= 0 || followUserDto.followed_user_id <= 0)
        {
           return BadRequest("Invalid user IDs provided.");
        }

        // Check if the following relationship exists
        var followRecord = _context.Followers
            .FirstOrDefault(f => f.followed_user_id == followUserDto.followed_user_id && f.follower_user_id == followUserDto.follower_user_id);

        if (followRecord == null)
        {
            return NotFound("You are not following this user.");
        }

        // Remove the follow record
        _context.Followers.Remove(followRecord);
        _context.SaveChanges();

        return Ok("Unfollowed successfully.");
    }


    // GET: api/Users/follower-requests
    [HttpGet("follower-requests")]
    public ActionResult GetFollowerRequests(int currentUserId)
    {
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{currentUserId}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Validate the input to ensure currentUserId is valid
        if (currentUserId <= 0)
        {
            return BadRequest("Invalid user ID.");
        }

        // Step 1: Get user IDs who are following the current user and not dismissed
        var followers = _context.Followers
            .Where(f => f.followed_user_id == currentUserId && !f.is_dismissed)
            .Select(f => f.follower_user_id)
            .ToList();

        // Step 2: Fetch user data for followers who are not followed back by the current user
        var pendingFollowers = _context.users
            .Where(u => followers.Contains(u.user_id) &&
                        !_context.Followers.Any(f => f.followed_user_id == u.user_id && f.follower_user_id == currentUserId))
            .Select(u => new UserConnection
            {
                user_id = u.user_id,
                fullname = u.fullname,
                username = u.username,
                profile_pic = u.profile_pic,
                bio = u.bio,
                phone_number = u.phone_number,
                is_following = _context.Followers.Any(f => f.followed_user_id == currentUserId && f.follower_user_id == u.user_id),
                am_following = false // Current user hasn't followed them back
            })
            .ToList();

        return Ok(pendingFollowers);
    }


    // POST: api/Users/cancel-follower-request
    [HttpPost("cancel-follower-request")]
    public ActionResult CancelFollowerRequest(FollowUserDto followUserDto)
    {
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{followUserDto.follower_user_id}:{followUserDto.followed_user_id}";

         // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Check if both user IDs are valid
        if (followUserDto.follower_user_id <= 0 || followUserDto.followed_user_id <= 0)
        {
            return BadRequest("Invalid user IDs provided.");
        }

        // Find the follow relationship in the Followers table where the current user is being followed
        var followRecord = _context.Followers
            .FirstOrDefault(f => f.followed_user_id == followUserDto.followed_user_id && f.follower_user_id == followUserDto.follower_user_id);

        if (followRecord == null)
        {
            return NotFound("No such follower request exists.");
        }

        // Mark the follow request as dismissed
        followRecord.is_dismissed = true;
        _context.SaveChanges();

        return Ok("Follower request dismissed.");
    }

    // PUT: api/Users/update-follower-status
    [HttpPut("update-follower-status")]
    [AllowAnonymous]
    public async Task<ActionResult> UpdateFollowerStatus([FromBody] UpdateFollowStatusDto updateFollowStatusDto)
    {
        /*
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{updateFollowStatusDto.follower_user_id}:{updateFollowStatusDto.followed_user_id}:{updateFollowStatusDto.approval_status}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }
        */

        // Input validation to ensure IDs are valid
        if (updateFollowStatusDto.follower_user_id <= 0 || updateFollowStatusDto.followed_user_id <= 0)
        {
            return BadRequest("Invalid user IDs provided.");
        }

        // Fetch the follow relationship
        var followRecord = _context.Followers
            .FirstOrDefault(f => f.followed_user_id == updateFollowStatusDto.followed_user_id && f.follower_user_id == updateFollowStatusDto.follower_user_id);

        if (followRecord == null)
        {
            return NotFound("Follow relationship not found.");
        }

        // Validate the approval_status
        var validStatuses = new[] { "approved", "pending", "declined" };
        if (!validStatuses.Contains(updateFollowStatusDto.approval_status.ToLower()))
        {
            return BadRequest("Invalid approval status. Allowed values are: approved, pending, declined.");
        }

        // Only the followed user can approve or decline the request
        var currentUserId = updateFollowStatusDto.followed_user_id; // Simulate the current user as the followed user
        if (followRecord.followed_user_id != currentUserId)
        {
            return Forbid("You are not authorized to update the status of this follow request.");
        }

        // Update the approval status
        followRecord.approval_status = updateFollowStatusDto.approval_status.ToLower();

        // Save the changes
        _context.SaveChanges();

        // **Notification Logic Delegated to the Service**
        if (followRecord.approval_status == "approved")
        {
            // Avoid sending notification to self
            if (updateFollowStatusDto.follower_user_id != updateFollowStatusDto.followed_user_id)
            {
                try
                {
                    await _notificationService.HandleAcceptFollowRequestNotificationAsync(
                        recipientUserId: updateFollowStatusDto.follower_user_id,
                        senderUserId: updateFollowStatusDto.followed_user_id
                    );
                }
                catch (Exception ex)
                {
                    // Handle the exception as needed (log or ignore)
                }
            }
        }

        return Ok($"Follow request {updateFollowStatusDto.approval_status} successfully.");
    }

    // GET: api/Users/pending-follow-requests
    [HttpGet("pending-follow-requests")]
    public ActionResult GetPendingFollowRequests(int currentUserId)
    {
        // Extract the signature from the request header
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{currentUserId}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Validate the input to ensure currentUserId is valid
        if (currentUserId <= 0)
        {
            return BadRequest("Invalid user ID.");
        }

        // Fetch follow requests where the approval status is 'pending' and not dismissed
        var pendingFollowers = _context.Followers
            .Where(f => f.followed_user_id == currentUserId && f.approval_status == "pending")
            .Select(f => new
            {
                f.follower_user_id,
                f.Follower.fullname,       // Get the follower's fullname
                f.Follower.profile_pic,    // Get the follower's profile picture
                f.Follower.username
            })
            .ToList();

        // Return a 200 OK response with an empty list if no pending followers are found
        return Ok(pendingFollowers);
    }
}

