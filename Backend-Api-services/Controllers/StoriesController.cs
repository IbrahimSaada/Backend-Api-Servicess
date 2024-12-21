using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services; // Import SignatureService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [CheckBan]
    public class StoriesController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;
        private readonly IBlockService _blockService;

        public StoriesController(
            apiDbContext context,
            SignatureService signatureService,
            IBlockService blockService)
        {
            _context = context;
            _signatureService = signatureService;
            _blockService = blockService;
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates a signature using the SignatureService.
        /// Returns true if valid; otherwise false.
        /// </summary>
        private bool ValidateRequestSignature(string dataToSign)
        {
            string signature = Request.Headers["X-Signature"];
            if (string.IsNullOrEmpty(signature))
            {
                return false;
            }
            return _signatureService.ValidateSignature(signature, dataToSign);
        }

        /// <summary>
        /// Gets a paginated subset of a query.
        /// </summary>
        private async Task<(List<T> results, int totalCount)> GetPaginatedResultAsync<T>(
            IQueryable<T> query,
            int pageIndex,
            int pageSize)
        {
            int totalCount = await query.CountAsync();
            // pageIndex is 1-based, so skip = (pageIndex - 1) * pageSize
            var pagedData = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedData, totalCount);
        }

        #endregion

        // POST: api/Stories
        [HttpPost]
        public async Task<IActionResult> PostStory([FromBody] StoriesRequest storyRequest)
        {
            // Validate signature
            var dataToSign = $"{storyRequest.user_id}:{string.Join(",", storyRequest.Media.Select(m => m.media_url))}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            // Check if there's an existing active story within a 24-hour window
            var existingStory = await _context.Stories
                .Include(s => s.Media)
                .FirstOrDefaultAsync(s => s.user_id == storyRequest.user_id && s.isactive);

            if (existingStory != null && existingStory.expiresat > DateTime.UtcNow)
            {
                // If existing story is active, just append media
                if (storyRequest.Media != null && storyRequest.Media.Any())
                {
                    existingStory.Media.AddRange(
                        storyRequest.Media.Select(m => new storiesmedia
                        {
                            media_url = m.media_url,
                            media_type = m.media_type,
                            stories = existingStory,
                            // expiresat, createdat auto-assigned by model
                        })
                    );

                    // Optionally extend expiresat
                    existingStory.expiresat = existingStory.Media.Max(m => m.expiresat);
                }

                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetStory), new { id = existingStory.story_id }, existingStory);
            }
            else
            {
                // No active story found, create a new one
                var newStory = new stories
                {
                    user_id = storyRequest.user_id,
                };

                if (storyRequest.Media != null && storyRequest.Media.Any())
                {
                    newStory.Media = storyRequest.Media.Select(m => new storiesmedia
                    {
                        media_url = m.media_url,
                        media_type = m.media_type,
                        stories = newStory
                    }).ToList();
                }

                _context.Stories.Add(newStory);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetStory), new { id = newStory.story_id }, newStory);
            }
        }

        // GET: api/Stories/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStory(int id)
        {
 
            // Validate signature
            var dataToSign = $"{id}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var story = await _context.Stories
                .AsNoTracking()          // read-only
                .Include(s => s.Media)
                .FirstOrDefaultAsync(s => s.story_id == id);

            if (story == null) return NotFound();

            var response = new StoriesResponse
            {
                story_id = story.story_id,
                user_id = story.user_id,
                createdat = story.createdat,
                expiresat = story.expiresat,
                isactive = story.isactive,
                viewscount = story.viewscount,
                Media = story.Media.Select(m => new StoriesMediaResponse
                {
                    media_id = m.media_id,
                    media_url = m.media_url,
                    media_type = m.media_type
                }).ToList()
            };

            return Ok(response);
        }

        // GET: api/Stories/user/{userId}
        // Pagination example: GET /api/Stories/user/12?pageIndex=1&pageSize=20
        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStories(
            int userId,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {
            /*
            // Validate signature
            var dataToSign = $"{userId}:{pageIndex}:{pageSize}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }
            */


            // Optional: check if the user exists
            bool userExists = await _context.users
                .AsNoTracking()
                .AnyAsync(u => u.user_id == userId);

            if (!userExists) return NotFound("User not found");

            // Fetch all active stories that have not expired
            var activeStoriesQuery = _context.Stories
                .AsNoTracking()
                .Include(s => s.Media)
                .Include(s => s.users)       // we need user info (fullname, profile_pic)
                .Where(s => s.isactive && s.expiresat > DateTime.UtcNow);

            // We'll do the blocking check in-memory if needed
            var allActiveStories = await activeStoriesQuery.ToListAsync();
            var responseList = new List<StoriesResponse>();

            // Fetch which stories have been viewed by user in one go
            var viewedStoryIds = await _context.StoryViews
                .AsNoTracking()
                .Where(v => v.viewer_id == userId)
                .Select(v => v.story_id)
                .ToListAsync();

            // Filter out blocked stories + expired media
            foreach (var story in allActiveStories)
            {
                // Check if the user is blocked by the story owner or vice versa
                var (isBlocked, _) = await _blockService.IsBlockedAsync(userId, story.user_id);
                if (isBlocked) continue;

                var validMedia = story.Media
                    .Where(m => m.expiresat > DateTime.UtcNow)
                    .Select(m => new StoriesMediaResponse
                    {
                        media_id = m.media_id,
                        media_url = m.media_url,
                        media_type = m.media_type,
                        expiresat = m.expiresat,
                        createdatforeachstory = m.createdat
                    })
                    .ToList();

                if (!validMedia.Any()) continue;

                var storyResponse = new StoriesResponse
                {
                    story_id = story.story_id,
                    user_id = story.user_id,
                    createdat = story.createdat,
                    expiresat = story.expiresat,
                    isactive = story.isactive,
                    viewscount = story.viewscount,
                    isviewed = viewedStoryIds.Contains(story.story_id),
                    fullname = story.users.fullname,
                    profile_pic = story.users.profile_pic,
                    Media = validMedia
                };
                responseList.Add(storyResponse);
            }

            // Now that we have the final filtered list, apply pagination in-memory.
            // Alternatively, you can transform your approach to do more of this filtering in the DB,
            // but with blocking checks, often we do it in memory.
            int totalCount = responseList.Count;
            var paginatedStories = responseList
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Return data + pagination metadata
            var paginatedResult = new
            {
                Data = paginatedStories,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return Ok(paginatedResult);
        }

        // POST: api/Stories/View
        [HttpPost("View")]
        public async Task<IActionResult> RecordStoryView([FromBody] StoryViewRequest viewRequest)
        {

            // Validate signature
            var dataToSign = $"{viewRequest.story_id}:{viewRequest.viewer_id}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }


            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Fetch the story to check its owner
            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.story_id == viewRequest.story_id);

            if (story == null)
            {
                return NotFound(new { message = "Story not found." });
            }

            // Check if the viewer is the owner of the story
            if (story.user_id == viewRequest.viewer_id)
            {
                return Ok(new { message = "Story owner is not counted as a viewer." });
            }

            // Check if the user has already viewed this story
            var existingView = await _context.StoryViews
                .FirstOrDefaultAsync(v => v.story_id == viewRequest.story_id && v.viewer_id == viewRequest.viewer_id);

            if (existingView != null)
            {
                // Already viewed
                return Ok(new { message = "Story already viewed by this user." });
            }

            // Record the view
            var storyView = new storyviews
            {
                story_id = viewRequest.story_id,
                viewer_id = viewRequest.viewer_id,
            };

            _context.StoryViews.Add(storyView);

            // Increment the viewscount
            story.viewscount++;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Story view recorded successfully." });
        }

        // GET: api/Stories/{storyId}/viewers
        // Pagination example: GET /api/Stories/123/viewers?pageIndex=1&pageSize=20
        [HttpGet("{storyId}/viewers")]
        public async Task<IActionResult> GetStoryViewers(
            int storyId,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {

            // Validate signature
            var dataToSign = $"{storyId}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Query all viewers
            var viewersQuery = _context.StoryViews
                .AsNoTracking()
                .Where(v => v.story_id == storyId)
                .Include(v => v.viewer) // for user info
                .Select(v => new StoryViewerResponse
                {
                    viewer_id = v.viewer_id,
                    fullname = v.viewer.fullname,
                    profile_pic = v.viewer.profile_pic,
                    viewed_at = v.viewedat
                })
                .OrderByDescending(x => x.viewed_at);
            // Example: show newest viewers first; or choose your sorting approach

            // Apply pagination
            var (results, totalCount) = await GetPaginatedResultAsync(viewersQuery, pageIndex, pageSize);

            // Return a consistent shape
            var paginatedViewers = new
            {
                Data = results,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount
            };
            return Ok(paginatedViewers);
        }

        // DELETE: api/Stories/Media/{storyMediaId}/{userId}
        [HttpDelete("Media/{storyMediaId}/{userId}")]
        public async Task<IActionResult> DeleteStoryMedia(int storyMediaId, int userId)
        {
            // Validate signature
            var dataToSign = $"{storyMediaId}:{userId}";
            if (!ValidateRequestSignature(dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var storyMedia = await _context.StoriesMedia.FindAsync(storyMediaId);
            if (storyMedia == null)
            {
                return NotFound("Story media not found.");
            }

            var story = await _context.Stories.FindAsync(storyMedia.story_id);
            if (story == null || story.user_id != userId)
            {
                return Forbid("You are not authorized to delete this story media.");
            }

            // Remove the story media
            _context.StoriesMedia.Remove(storyMedia);
            await _context.SaveChangesAsync();

            // Check if this was the last media for the story
            var remainingMediaCount = await _context.StoriesMedia
                .Where(sm => sm.story_id == storyMedia.story_id) // Check media for the same story
                .CountAsync();

            if (remainingMediaCount == 0)
            {
                // If no media left, set the story's is_active to false
                story.isactive = false; // Assuming `is_active` is the field in the Stories table
                _context.Stories.Update(story);
                await _context.SaveChangesAsync();
            }

            return Ok("Story media deleted successfully.");
        }

    }
}