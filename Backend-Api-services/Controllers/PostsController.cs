using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
[Authorize]  // Apply JWT authorization to all endpoints in this controller
[CheckBan]
public class PostsController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly SignatureService _signatureService;
    private readonly INotificationService _notificationService;
    private readonly IBlockService _blockService;

    public PostsController(apiDbContext context, SignatureService signatureService, INotificationService notificationService, IBlockService blockService)
    {
        _context = context;
        _signatureService = signatureService;
        _notificationService = notificationService;
        _blockService = blockService;
    }

    // GET: api/Posts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts(int userId)
    {
        var posts = await _context.Posts
                                  .Include(p => p.User)
                                  .Include(p => p.Media)
                                  .Where(p => p.is_public)
                                  .OrderByDescending(p => p.created_at)
                                  .ToListAsync();

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
            is_liked = _context.Likes.Any(like => like.post_id == post.post_id && like.user_id == userId),
            is_Bookmarked = _context.Bookmarks.Any(bookmark => bookmark.post_id == post.post_id && bookmark.user_id == userId)
        }).ToList();

        return Ok(postResponses);
    }

    // POST: api/Posts/Like
    [HttpPost("Like")]
    public async Task<IActionResult> LikePost([FromBody] LikeRequest likeRequest)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{likeRequest.user_id}:{likeRequest.post_id}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        var postId = likeRequest.post_id;
        var userId = likeRequest.user_id;

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(likeRequest.user_id, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
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
        post.like_count += 1;

        await _context.SaveChangesAsync();

        // **Notification Logic Delegated to the Service**
        var postOwnerId = post.user_id;
        if (postOwnerId != userId)
        {
            await _notificationService.HandleAggregatedNotificationAsync(
                recipientUserId: postOwnerId,
                senderUserId: userId,
                type: "Like",
                relatedEntityId: postId,
                action: "liked"
            );
        }

        return Ok("Post liked successfully.");
    }

    // POST: api/Posts/Unlike
    [HttpPost("Unlike")]
    public async Task<IActionResult> UnlikePost([FromBody] LikeRequest likeRequest)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{likeRequest.user_id}:{likeRequest.post_id}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

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

        _context.Likes.Remove(like);
        post.like_count -= 1;

        await _context.SaveChangesAsync();

        return Ok("Like removed successfully.");
    }

    [HttpPost("{postId}/Commenting")]
    public async Task<IActionResult> AddComment(int postId, [FromBody] CommentRequest commentRequest)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{commentRequest.userid}:{postId}:{commentRequest.text}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(commentRequest.userid, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
        }

        var comment = new Comment
        {
            post_id = postId,
            user_id = commentRequest.userid,
            parent_comment_id = commentRequest.parentcommentid,
            text = commentRequest.text
        };

        _context.Comments.Add(comment);
        post.comment_count += 1;

        await _context.SaveChangesAsync();

        // **Notification Logic Delegated to the Service**

        int recipientUserId = 0;
        string notificationType;
        string notificationMessage;

        // Identify whether this is a top-level comment or a reply
        var sender = await _context.users.FirstOrDefaultAsync(u => u.user_id == commentRequest.userid);
        var senderFullName = sender?.fullname ?? "Someone";

        if (commentRequest.parentcommentid == null)
        {
            // Top-level comment
            recipientUserId = post.user_id;
            notificationType = "Comment";
            notificationMessage = $"{senderFullName} commented on your post.";
        }
        else
        {
            // Reply to a comment
            var parentComment = await _context.Comments.FindAsync(commentRequest.parentcommentid);
            if (parentComment != null)
            {
                recipientUserId = parentComment.user_id;
                notificationType = "Reply";
                notificationMessage = $"{senderFullName} replied to your comment.";
            }
            else
            {
                // If parent comment doesn't exist, no notification is sent
                // (Alternatively, return a NotFound error if needed)
                return CreatedAtAction(nameof(GetComments), new { postId = postId }, new { CommentId = comment.comment_id });
            }
        }

        // Avoid sending notification to self
        if (recipientUserId != 0 && recipientUserId != commentRequest.userid)
        {
            try
            {
                // Here we assume we have aggregator logic similar to ***REMOVED***s:
                // For comments, we might have a method like HandleCommentNotificationAsync
                // that handles both "Comment" and "Reply" notification aggregation.
                // You will need to implement this method in the NotificationService similarly
                // to how ***REMOVED*** was done.

                await _notificationService.HandleCommentNotificationAsync(
                    recipientUserId: recipientUserId,
                    senderUserId: commentRequest.userid,
                    postId: postId,
                    commentId: comment.comment_id,
                    notificationType: notificationType
                );
            }
            catch (Exception ex)
            {
                // Log the exception
                //_logger.LogError(ex, "Failed to handle comment notification.");
                // We do not fail the request just because the notification didn't send
            }
        }

        return CreatedAtAction(nameof(GetComments), new { postId = postId }, new { CommentId = comment.comment_id });
    }

    // GET: api/Posts/{postId}/Comments
    [HttpGet("{postId}/Comments")]
    [AllowAnonymous]
    public async Task<ActionResult<PaginatedCommentResponse>> GetComments(
        int postId,
        int pageNumber = 1,
        int pageSize = 5)
    {
        // 1) Basic validation for pagination parameters
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 5;

        // 2) Query all top-level comments for the given post
        var commentsQuery = _context.Comments
                                    .Include(c => c.User)
                                    .Where(c => c.post_id == postId && c.parent_comment_id == null)
                                    .OrderByDescending(c => c.created_at);

        // 3) Count total records (before pagination)
        var totalCount = await commentsQuery.CountAsync();

        // 4) Apply pagination (Skip / Take)
        var comments = await commentsQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 5) Map those comments to DTOs (including nested replies, which are not paginated here)
        var commentResponses = comments.Select(c => new CommentResponse
        {
            commentid = c.comment_id,
            postid = c.post_id,
            userid = c.user_id,
            fullname = c.User.fullname,
            userprofilepic = c.User.profile_pic,
            text = c.text,
            created_at = c.created_at,
            Replies = GetReplies(c.comment_id).ToList()
        }).ToList();

        // 6) Create a PaginatedCommentResponse to include metadata (total count, pages, etc.)
        var response = new PaginatedCommentResponse
        {
            Comments = commentResponses,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
        };

        return Ok(response);
    }

    private IEnumerable<CommentResponse> GetReplies(int parentCommentId)
    {
        var replies = _context.Comments
                              .Include(c => c.User)
                              .Where(c => c.parent_comment_id == parentCommentId)
                              .OrderBy(c => c.created_at)
                              .ToList();

        return replies.Select(c => new CommentResponse
        {
            commentid = c.comment_id,
            postid = c.post_id,
            userid = c.user_id,
            fullname = c.User.fullname,
            userprofilepic = c.User.profile_pic,
            text = c.text,
            created_at = c.created_at,
            Replies = GetReplies(c.comment_id).ToList()
        });
    }

    // PUT: api/Posts/{postId}/Comments/{commentId}
    [HttpPut("{postId}/Comments/{commentId}")]
    public async Task<IActionResult> EditComment(int postId, int commentId, [FromBody] CommentRequest commentRequest)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{commentRequest.userid}:{postId}:{commentRequest.text}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(commentRequest.userid, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
        }


        var comment = await _context.Comments.FindAsync(commentId);
        if (comment == null || comment.post_id != postId)
        {
            return NotFound("Comment not found.");
        }

        if (comment.user_id != commentRequest.userid)
        {
            return BadRequest("You are not authorized to edit this comment.");
        }

        comment.text = commentRequest.text;

        await _context.SaveChangesAsync();

        return Ok("Comment updated successfully.");
    }

    // DELETE: api/Posts/{postId}/Comments/{commentId}
    [HttpDelete("{postId}/Comments/{commentId}")]
    public async Task<IActionResult> DeleteComment(int postId, int commentId, [FromQuery] int userId)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{userId}:{postId}:{commentId}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var comment = await _context.Comments.FindAsync(commentId);
        if (comment == null || comment.post_id != postId)
        {
            return NotFound("Comment not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(userId, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
        }


        if (comment.user_id != userId)
        {
            return BadRequest("You are not authorized to delete this comment.");
        }

        // Get the total number of deleted comments including nested replies
        int deletedCommentsCount = await DeleteCommentAndReplies(commentId);

        // Decrement the comment count in the post by the number of deleted comments
        post.comment_count -= deletedCommentsCount;

        await _context.SaveChangesAsync();

        return Ok($"Comment and its nested replies deleted successfully. Total comments deleted: {deletedCommentsCount}");
    }

    private async Task<int> DeleteCommentAndReplies(int commentId)
    {
        int deletedCount = 1;  // Start with 1 for the current comment

        var nestedComments = await _context.Comments
            .Where(c => c.parent_comment_id == commentId)
            .ToListAsync();

        foreach (var nestedComment in nestedComments)
        {
            deletedCount += await DeleteCommentAndReplies(nestedComment.comment_id);
        }

        var comment = await _context.Comments.FindAsync(commentId);
        if (comment != null)
        {
            _context.Comments.Remove(comment);
        }

        return deletedCount;
    }
    // POST: api/Posts/Bookmark
    [HttpPost("Bookmark")]
    public async Task<IActionResult> BookmarkPost([FromBody] BookmarkRequest BookmarkRequest)
    {
         var signature = Request.Headers["X-Signature"].FirstOrDefault();
         var dataToSign = $"{BookmarkRequest.user_id}:{BookmarkRequest.post_id}";

         // Validate the signature
         if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
         {
           return Unauthorized("Invalid or missing signature.");
         }

        var postId = BookmarkRequest.post_id;
        var userId = BookmarkRequest.user_id;

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(BookmarkRequest.user_id, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
        }

        var existingBookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(l => l.post_id == postId && l.user_id == userId);

        if (existingBookmark != null)
        {
            return BadRequest("You have already bookmark this post.");
        }

        var bookmark = new Bookmark
        {
            post_id = postId,
            user_id = userId
        };

        _context.Bookmarks.Add(bookmark);
        post.bookmark_count++;

        await _context.SaveChangesAsync();

        return Ok("Post bookmarked successfully.");
    }
    // POST: api/Posts/Unbookmark
    [HttpPost("Unbookmark")]
    public async Task<IActionResult> UnbookmarkPost([FromBody] BookmarkRequest bookmarkRequest)
    {
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{bookmarkRequest.user_id}:{bookmarkRequest.post_id}";

         // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        var postId = bookmarkRequest.post_id;
        var userId = bookmarkRequest.user_id;

        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        var (isBlocked, reason) = await _blockService.IsBlockedAsync(bookmarkRequest.user_id, post.user_id);

        if (isBlocked)
        {
            // Return the 403 status with the blocking reason
            return StatusCode(403, reason);
        }

        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(l => l.post_id == postId && l.user_id == userId);

        if (bookmark == null)
        {
            return BadRequest("You have not bookmark this post.");
        }

        _context.Bookmarks.Remove(bookmark);
        post.bookmark_count--;

        await _context.SaveChangesAsync();

        return Ok("Bookmark removed successfully.");
    }

    [HttpGet("{postId}/Comments/Threads")]
    public async Task<ActionResult<List<CommentResponse>>> GetCommentThreadsByIds(int postId, [FromQuery] string ids)
    {
        // Validate input
        if (string.IsNullOrEmpty(ids))
        {
            return BadRequest("No comment IDs provided.");
        }
        // Validate the signature (optional, if required in your use case)
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{ids}";
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }


        // Parse and validate the comment IDs
        var commentIds = ids.Split(',')
            .Select(id => id.Trim())
            .Where(id => int.TryParse(id, out _))
            .Select(int.Parse)
            .ToList();

        if (!commentIds.Any())
        {
            return BadRequest("No valid comment IDs provided.");
        }

        // Fetch all matching comments for the given post
        var comments = await _context.Comments
            .Include(c => c.User)
            .Where(c => commentIds.Contains(c.comment_id) && c.post_id == postId)
            .ToListAsync();

        if (!comments.Any())
        {
            return NotFound("No comments found for the provided IDs.");
        }

        // Build threads for each comment
        var threads = new List<CommentResponse>();
        foreach (var comment in comments)
        {
            var thread = await BuildMinimalCommentThread(comment);
            threads.Add(thread);
        }

        return Ok(threads);
    }

    // Helper method remains unchanged
    private async Task<CommentResponse> BuildMinimalCommentThread(Comment comment)
    {
        var commentResponse = new CommentResponse
        {
            commentid = comment.comment_id,
            postid = comment.post_id,
            userid = comment.user_id,
            fullname = comment.User.fullname,
            userprofilepic = comment.User.profile_pic,
            text = comment.text,
            created_at = comment.created_at,
            isHighlighted = true,
            Replies = new List<CommentResponse>(),
            ParentComment = null
        };

        // Fetch the parent comment if it exists
        if (comment.parent_comment_id.HasValue)
        {
            var parentComment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.comment_id == comment.parent_comment_id.Value);

            if (parentComment != null)
            {
                commentResponse.ParentComment = new CommentResponse
                {
                    commentid = parentComment.comment_id,
                    postid = parentComment.post_id,
                    userid = parentComment.user_id,
                    fullname = parentComment.User.fullname,
                    userprofilepic = parentComment.User.profile_pic,
                    text = parentComment.text,
                    created_at = parentComment.created_at,
                    isHighlighted = false,
                    Replies = null,
                    ParentComment = null
                };
            }
        }

        // Fetch the latest reply to this comment
        var latestReply = await _context.Comments
            .Include(c => c.User)
            .Where(c => c.parent_comment_id == comment.comment_id)
            .OrderByDescending(c => c.created_at)
            .FirstOrDefaultAsync();

        if (latestReply != null)
        {
            var replyResponse = new CommentResponse
            {
                commentid = latestReply.comment_id,
                postid = latestReply.post_id,
                userid = latestReply.user_id,
                fullname = latestReply.User.fullname,
                userprofilepic = latestReply.User.profile_pic,
                text = latestReply.text,
                created_at = latestReply.created_at,
                isHighlighted = false,
                Replies = null,
                ParentComment = null
            };

            commentResponse.Replies.Add(replyResponse);
        }

        return commentResponse;
    }

    // GET: api/Posts/{postId}/Likes
    [HttpGet("{postId}/Likes")]
    public async Task<IActionResult> GetPostLikes(int postId)
    {
        // Extract the signature from headers
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        var dataToSign = $"{postId}";

        // Validate the signature
        if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
        {
            return Unauthorized("Invalid or missing signature.");
        }

        // Validate if the post exists
        var post = await _context.Posts.FindAsync(postId);
        if (post == null)
        {
            return NotFound("Post not found.");
        }

        // Fetch all users who liked the post
        var likes = await _context.Likes
            .Where(like => like.post_id == postId)
            .Include(like => like.User)
            .ToListAsync();

        // Map the likes to a response object
        var response = likes.Select(like => new
        {
            user_id = like.User.user_id,
            fullname = like.User.fullname,
            profile_pic = like.User.profile_pic,
            liked_at = like.created_at // If you store the timestamp for when the like occurred
        }).ToList();

        return Ok(response);
    }

}
