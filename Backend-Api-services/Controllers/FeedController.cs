using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.DTOs.feedDto;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;

        public FeedController(apiDbContext context, SignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FeedItemResponse>>> GetFeed(int userId, int pageNumber = 1, int pageSize = 20)
        {
            var feedItems = await GetFeedItems(userId);

            // Order by CreatedAt descending
            var sortedItems = feedItems.OrderByDescending(item => item.CreatedAt);

            // Implement pagination
            var pagedItems = sortedItems.Skip((pageNumber - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToList();

            return Ok(pagedItems);
        }

        // Helper Methods

        private async Task<IEnumerable<FeedItemResponse>> GetFeedItems(int userId)
        {
            var posts = await GetPosts(userId);
            var sharedPosts = await GetSharedPosts(userId);

            // Combine the posts and shared posts
            return posts.Concat(sharedPosts);
        }


        private async Task<IEnumerable<FeedItemResponse>> GetPosts(int userId)
        {
            var approvedFollowings = GetApprovedFollowings(userId);

            var userLikedPostIds = GetUserLikedPostIds(userId);
            var userBookmarkedPostIds = GetUserBookmarkedPostIds(userId);

            var postsQuery = _context.Posts.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Media)
                .Where(p =>
                    p.is_public ||
                    p.user_id == userId ||
                    approvedFollowings.Contains(p.user_id))
                .Select(post => new FeedItemResponse
                {
                    Type = "post",
                    ItemId = post.post_id,
                    CreatedAt = post.created_at,
                    Content = post.caption,
                    User = new UserInfo
                    {
                        UserId = post.User.user_id,
                        FullName = post.User.fullname,
                        Username = post.User.username,
                        ProfilePictureUrl = post.User.profile_pic
                    },
                    // For posts, we don't include PostInfo since there's no additional author
                    IsLiked = userLikedPostIds.Contains(post.post_id),
                    IsBookmarked = userBookmarkedPostIds.Contains(post.post_id)
                });

            // Since we removed 'post' from the FeedItemResponse for posts, we need to manually map PostInfo
            var posts = await postsQuery.ToListAsync();

            // Add PostInfo to each post
            foreach (var feedItem in posts)
            {
                feedItem.Post = new PostInfo
                {
                    PostId = feedItem.ItemId,
                    CreatedAt = feedItem.CreatedAt,
                    Content = feedItem.Content,
                    Media = _context.PostMedias.AsNoTracking()
                        .Where(m => m.post_id == feedItem.ItemId)
                        .Select(media => new PostMediaResponse
                        {
                            media_id = media.media_id,
                            media_url = media.media_url,
                            media_type = media.media_type,
                            post_id = media.post_id,
                            thumbnail_url = media.thumbnail_url
                        }).ToList(),
                    LikeCount = _context.Posts.AsNoTracking()
                        .Where(p => p.post_id == feedItem.ItemId)
                        .Select(p => p.like_count)
                        .FirstOrDefault(),
                    CommentCount = _context.Posts.AsNoTracking()
                        .Where(p => p.post_id == feedItem.ItemId)
                        .Select(p => p.comment_count)
                        .FirstOrDefault()
                    // No need to include Author here since it's the same as User
                };
            }

            return posts;
        }



        private async Task<IEnumerable<FeedItemResponse>> GetSharedPosts(int userId)
        {
            var approvedFollowings = GetApprovedFollowings(userId);

            var userLikedPostIds = GetUserLikedPostIds(userId);
            var userBookmarkedPostIds = GetUserBookmarkedPostIds(userId);

            var sharedPostsQuery = _context.SharedPosts.AsNoTracking()
                .Include(sp => sp.Sharedby)
                .Include(sp => sp.PostContent)
                    .ThenInclude(p => p.Media)
                .Include(sp => sp.PostContent.User)
                .Where(sp =>
                    sp.PostContent.is_public ||
                    sp.PostContent.user_id == userId ||
                    approvedFollowings.Contains(sp.PostContent.user_id) ||
                    sp.SharerId == userId)
                .Select(sharedPost => new FeedItemResponse
                {
                    Type = "repost",
                    ItemId = sharedPost.ShareId,
                    CreatedAt = sharedPost.SharedAt,
                    Content = sharedPost.Comment, // Sharer's comment
                    User = new UserInfo
                    {
                        UserId = sharedPost.Sharedby.user_id,
                        FullName = sharedPost.Sharedby.fullname,
                        Username = sharedPost.Sharedby.username,
                        ProfilePictureUrl = sharedPost.Sharedby.profile_pic
                    },
                    // Include PostInfo with Author for reposts
                    Post = new PostInfo
                    {
                        PostId = sharedPost.PostContent.post_id,
                        CreatedAt = sharedPost.PostContent.created_at,
                        Content = sharedPost.PostContent.caption,
                        Media = sharedPost.PostContent.Media.Select(media => new PostMediaResponse
                        {
                            media_id = media.media_id,
                            media_url = media.media_url,
                            media_type = media.media_type,
                            post_id = media.post_id,
                            thumbnail_url = media.thumbnail_url
                        }).ToList(),
                        LikeCount = sharedPost.PostContent.like_count,
                        CommentCount = sharedPost.PostContent.comment_count,
                        Author = new UserInfo
                        {
                            UserId = sharedPost.PostContent.User.user_id,
                            FullName = sharedPost.PostContent.User.fullname,
                            Username = sharedPost.PostContent.User.username,
                            ProfilePictureUrl = sharedPost.PostContent.User.profile_pic
                        }
                    },
                    IsLiked = userLikedPostIds.Contains(sharedPost.PostId),
                    IsBookmarked = userBookmarkedPostIds.Contains(sharedPost.PostId)
                });

            return await sharedPostsQuery.ToListAsync();
        }




        private IQueryable<int> GetApprovedFollowings(int userId)
        {
            return _context.Followers.AsNoTracking()
                .Where(f =>
                    f.follower_user_id == userId &&
                    f.approval_status == "approved")
                .Select(f => f.followed_user_id);
        }

        private IQueryable<int> GetUserLikedPostIds(int userId)
        {
            return _context.Likes.AsNoTracking()
                .Where(like => like.user_id == userId)
                .Select(like => like.post_id);
        }

        private IQueryable<int> GetUserBookmarkedPostIds(int userId)
        {
            return _context.Bookmarks.AsNoTracking()
                .Where(bookmark => bookmark.user_id == userId)
                .Select(bookmark => bookmark.post_id);
        }
    }
}
