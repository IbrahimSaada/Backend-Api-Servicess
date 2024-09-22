﻿using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
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

    public PostsController(apiDbContext context, SignatureService signatureService)
    {
        _context = context;
        _signatureService = signatureService;
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

    // POST: api/Posts/{postId}/Commenting
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

}
