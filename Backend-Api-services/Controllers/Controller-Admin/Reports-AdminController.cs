using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.DTOs.feedDto;
using Backend_Api_services.Models.DTOs.questionDto;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Backend_Api_services.Controllers.Controller_Admin
{
    [ApiController]
    [Route("api/[controller]")]
    public class Reports_AdminController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;

        public Reports_AdminController(apiDbContext context, SignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }
        // GET: api/Reports
        [HttpGet("GetReports")]
        public async Task<ActionResult<IEnumerable<ReportRequest>>> GetReports([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest("Page and pageSize must be greater than zero.");

            // Validate signature, etc.

            var query = _context.Reports
                .Include(r => r.ReportedBy)
                .Include(r => r.ReportedUser)
                .OrderByDescending(r => r.created_at); // Apply ordering before pagination

            var totalReports = await query.CountAsync();
            var reports = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = reports.Select(r => new ReportRequest
            {
                ReportId = r.report_id,
                ReportedBy = r.reported_by,
                ReportedByUsername = r.ReportedBy.username,
                ReportedUser = r.reported_user,
                ReportedUserUsername = r.ReportedUser.username,
                content_type = r.content_type,
                ContentId = r.content_id,
                ReportReason = r.report_reason,
                ReportStatus = r.report_status,
                SeverityLevel = r.severity_level,
                CreatedAt = r.created_at,
                ResolvedAt = r.resolved_at
            })
            .ToList();

            var response = new
            {
                TotalReports = totalReports,
                Page = page,
                PageSize = pageSize,
                Reports = reportDtos
            };

            return Ok(response);
        }

        [HttpGet("CountUnresolved")]
        public async Task<ActionResult<int>> CountUnresolvedReports()
        {
            // Count the unresolved reports (e.g., assuming "unresolved" means a specific status value)
            var unresolvedCount = await _context.Reports
                .Where(r => r.report_status == "Pending") // Adjust based on your actual status value
                .CountAsync();

            return Ok(unresolvedCount);
        }

        [HttpPost("ResolveReport/{reportId}")]
        public async Task<ActionResult> ResolveReport(int reportId)
        {
            // Validate the reportId
            if (reportId <= 0)
                return BadRequest("Invalid report ID.");

            // Fetch the report from the database
            var report = await _context.Reports.FindAsync(reportId);

            if (report == null)
                return NotFound($"No report found with ID {reportId}.");

            if (report.report_status == "Resolved")
                return BadRequest("The report is already resolved.");

            // Update the report status and resolved timestamp
            report.report_status = "Resolved";
            report.resolved_at = DateTime.UtcNow;

            // Save changes to the database
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Report resolved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while resolving the report: {ex.Message}");
            }
        }
        // GET: api/Stories/Specific/{storyId}
        [HttpGet("Specific/{storyId}")]
        public async Task<IActionResult> GetSpecificStory(int storyId)
        {
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            // Validate signature
            var dataToSign = $"{storyId}";
            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized(new { message = "Invalid or missing signature." });
            }

            // Fetch the specific story along with its media
            var story = await _context.Stories
                .AsNoTracking()
                .Include(s => s.Media)
                .Include(s => s.users) // Include user for additional details like name and profile picture
                .FirstOrDefaultAsync(s => s.story_id == storyId);

            if (story == null)
            {
                return NotFound(new { message = "Story not found." });
            }

            // Map the story data to a response DTO
            var response = new StoriesResponse
            {
                story_id = story.story_id,
                user_id = story.user_id,
                createdat = story.createdat,
                expiresat = story.expiresat,
                isactive = story.isactive,
                viewscount = story.viewscount,
                fullname = story.users?.fullname,
                profile_pic = story.users?.profile_pic,
                Media = story.Media.Select(m => new StoriesMediaResponse
                {
                    media_id = m.media_id,
                    media_url = m.media_url,
                    media_type = m.media_type,
                    createdatforeachstory = m.createdat,
                    expiresat = m.expiresat
                }).ToList()
            };

            return Ok(response);
        }
        [HttpGet("Post/{postId}")]
        public async Task<ActionResult<FeedItemResponse>> GetPostById(int postId, int userId)
        {

            // Extract the signature from headers
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            var dataToSign = $"{postId}:{userId}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Check if the post exists
            var post = await _context.Posts.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Media)
                .Where(p => p.post_id == postId)
                .Select(p => new FeedItemResponse
                {
                    Type = "post",
                    ItemId = p.post_id,
                    CreatedAt = p.created_at,
                    Content = p.caption,
                    User = new UserInfo
                    {
                        UserId = p.User.user_id,
                        FullName = p.User.fullname,
                        Username = p.User.username,
                        ProfilePictureUrl = p.User.profile_pic
                    },
                    Post = new PostInfo
                    {
                        PostId = p.post_id,
                        CreatedAt = p.created_at,
                        Content = p.caption,
                        Media = p.Media.Select(media => new PostMediaResponse
                        {
                            media_id = media.media_id,
                            media_url = media.media_url,
                            media_type = media.media_type,
                            post_id = media.post_id,
                            thumbnail_url = media.thumbnail_url
                        }).ToList(),
                        LikeCount = p.like_count,
                        CommentCount = p.comment_count
                    },
                    IsLiked = _context.Likes.Any(like => like.post_id == p.post_id && like.user_id == userId),
                    IsBookmarked = _context.Bookmarks.Any(bookmark => bookmark.post_id == p.post_id && bookmark.user_id == userId)
                })
                .FirstOrDefaultAsync();

            if (post == null)
                return NotFound(new { message = "Post not found." });

            // Check block


            return Ok(post);
        }

        [HttpGet("{userId}/question/{questionId}")]
        public async Task<IActionResult> GetSpecificQuestion(int userId, int questionId)
        {
            // Retrieve the signature from the request headers for security
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            var dataToSign = $"userId:{userId}|questionId:{questionId}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized(new { message = "Invalid or missing signature." });
            }

            // Fetch the question along with its metadata
            var question = await _context.questions
                .AsNoTracking()
                .Include(q => q.user) // Include user details
                .Include(q => q.questionmedia) // Include question media
                .Include(q => q.answers) // Include answers
                .FirstOrDefaultAsync(q => q.question_id == questionId);

            if (question == null)
            {
                return NotFound(new { message = $"Question with ID {questionId} not found." });
            }

            // Check if the user has liked the question
            var isLiked = await _context.question_likes
                .AsNoTracking()
                .AnyAsync(ql => ql.question_id == questionId && ql.user_id == userId);

            // Map the question details to a DTO
            var questionDto = new QuestionResponseDto
            {
                QuestionId = question.question_id,
                CreatedAt = question.created_at,
                Content = question.caption,
                User = new UserInfo
                {
                    UserId = question.user.user_id,
                    Username = question.user.username,
                    FullName = question.user.fullname,
                    ProfilePictureUrl = question.user.profile_pic
                },
                IsLiked = isLiked,
                IsVerified = _context.answers.Any(a => a.question_id == question.question_id && a.is_verified),
                Media = question.questionmedia.Select(media => new QuestionMediaDto
                {
                    MediaUrl = media.media_url,
                    MediaType = media.media_type
                }).ToList(),
                LikesCount = question.likes_count ?? 0,
                AnswersCount = question.answers_count ?? 0,
            };

            return Ok(questionDto);
        }
    }
}
