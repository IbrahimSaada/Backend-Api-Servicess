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
public class PostsController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly SignatureService _signatureService;
    private readonly INotificationService _notificationService;

    public PostsController(apiDbContext context, SignatureService signatureService, INotificationService notificationService)
    {
        _context = context;
        _signatureService = signatureService;
        _notificationService = notificationService;
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
        string notificationType = "";
        string notificationMessage = "";

        var sender = await _context.users.FirstOrDefaultAsync(u => u.user_id == commentRequest.userid);
        string senderFullName = sender?.fullname ?? "Someone";

        if (commentRequest.parentcommentid == null)
        {
            // It's a top-level comment
            recipientUserId = post.user_id;
            notificationType = "Comment";
            notificationMessage = $"{senderFullName} commented on your post.";
        }
        else
        {
            // It's a reply to a comment
            var parentComment = await _context.Comments.FindAsync(commentRequest.parentcommentid);
            if (parentComment != null)
            {
                recipientUserId = parentComment.user_id;
                notificationType = "Reply";
                notificationMessage = $"{senderFullName} replied to your comment.";
            }
        }

        // Avoid sending notification to self
        if (recipientUserId != 0 && recipientUserId != commentRequest.userid)
        {
            try
            {
                await _notificationService.SendAndSaveNotificationAsync(
                    recipientUserId: recipientUserId,
                    senderUserId: commentRequest.userid,
                    type: notificationType,
                    relatedEntityId: postId,
                    commentId: comment.comment_id,
                    message: notificationMessage
                );
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                
            }
        }

        return CreatedAtAction(nameof(GetComments), new { postId = postId }, new { CommentId = comment.comment_id });
    }

    // GET: api/Posts/{postId}/Comments
    [HttpGet("{postId}/Comments")]
    public async Task<ActionResult<IEnumerable<CommentResponse>>> GetComments(int postId)
    {
        var comments = await _context.Comments
                                      .Include(c => c.User)
                                      .Where(c => c.post_id == postId && c.parent_comment_id == null)
                                      .OrderByDescending(c => c.created_at)
                                      .ToListAsync();

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

        return Ok(commentResponses);
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

    // Adjusted API method for showing specific replies and parent
    [HttpGet("{postId}/Comments/{commentId}/Thread")]
    public async Task<ActionResult<CommentResponse>> GetCommentThread(int postId, int commentId)
    {
        // Fetch the comment with the given commentId and postId
        var comment = await _context.Comments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.comment_id == commentId && c.post_id == postId);

        if (comment == null)
        {
            return NotFound("Comment not found.");
        }

        // Build the minimal thread structure
        var minimalThread = await BuildMinimalCommentThread(comment);

        return Ok(minimalThread);
    }

    // Helper method to fetch the specific comment, its parent, and the last reply
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
            isHighlighted = true, // Always highlight the requested comment
            Replies = new List<CommentResponse>(),
            ParentComment = null
        };

        // If the comment has a parent, fetch the parent
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
                    isHighlighted = false, // Parent comment is not highlighted
                    Replies = null,
                    ParentComment = null
                };
            }
        }

        // If the comment is a parent, fetch the latest reply
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
                isHighlighted = false, // Replies are not highlighted
                Replies = null,
                ParentComment = null
            };

            commentResponse.Replies.Add(replyResponse);
        }

        return commentResponse;
    }

}
