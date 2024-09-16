using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services; // Import SignatureService
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StoriesController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService; // Inject SignatureService

        public StoriesController(apiDbContext context, SignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService; // Assign SignatureService
        }

        // POST: api/Stories
        [HttpPost]
        public async Task<IActionResult> PostStory([FromBody] StoriesRequest storyRequest)
        {
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{storyRequest.user_id}:{string.Join(",", storyRequest.Media.Select(m => m.media_url))}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if there's an existing active story for the user within the 24-hour window
            var existingStory = await _context.Stories
                                              .Include(s => s.Media)
                                              .FirstOrDefaultAsync(s => s.user_id == storyRequest.user_id && s.isactive);

            // If an existing active story is found, associate new media with it
            if (existingStory != null && existingStory.expiresat > DateTime.UtcNow)
            {
                if (storyRequest.Media != null && storyRequest.Media.Any())
                {
                    existingStory.Media.AddRange(storyRequest.Media.Select(m => new storiesmedia
                    {
                        media_url = m.media_url,
                        media_type = m.media_type,
                        stories = existingStory
                    }).ToList());

                    // Update the expiresat to extend it, if needed
                    existingStory.expiresat = existingStory.Media.Max(m => m.expiresat);
                }

                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetStory), new { id = existingStory.story_id }, existingStory);
            }
            else
            {
                // No active story found, create a new story
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
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{id}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var story = await _context.Stories
                                      .Include(s => s.Media)
                                      .FirstOrDefaultAsync(s => s.story_id == id);

            if (story == null)
            {
                return NotFound();
            }

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
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetStories(int userId)
        {
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{userId}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Optional: Check if the user exists to avoid unnecessary processing
            var userExists = await _context.users.AnyAsync(u => u.user_id == userId);
            if (!userExists)
            {
                return NotFound("User not found");
            }

            // Fetch all active stories that have not expired, including the user information
            var activeStories = await _context.Stories
                                              .Include(s => s.Media)
                                              .Include(s => s.users) // Include the associated user
                                              .Where(s => s.isactive && s.expiresat > DateTime.UtcNow)
                                              .ToListAsync();

            // Fetch all the view data for this user in one query
            var viewedStoryIds = await _context.StoryViews
                                               .Where(v => v.viewer_id == userId)
                                               .Select(v => v.story_id)
                                               .ToListAsync();

            // Create the response list with the isviewed status, fullname, and profile_pic
            var responseList = activeStories.Select(story => new StoriesResponse
            {
                story_id = story.story_id,
                user_id = story.user_id,
                createdat = story.createdat,
                expiresat = story.expiresat,
                isactive = story.isactive,
                viewscount = story.viewscount,
                isviewed = viewedStoryIds.Contains(story.story_id),
                fullname = story.users.fullname,  // Get the user's full name
                profile_pic = story.users.profile_pic,  // Get the user's profile picture
                Media = story.Media
                             .Where(m => m.expiresat > DateTime.UtcNow) // Only include unexpired media
                             .Select(m => new StoriesMediaResponse
                             {
                                 media_id = m.media_id,
                                 media_url = m.media_url,
                                 media_type = m.media_type,
                                 expiresat = m.expiresat,// Include expiresat for frontend reference
                                 createdatforeachstory = m.createdat
                             }).ToList()
            }).Where(r => r.Media.Any()) // Only return stories that have at least one unexpired media item
            .ToList();

            return Ok(responseList);
        }

        // POST: api/Stories/View
        [HttpPost("View")]
        public async Task<IActionResult> RecordStoryView([FromBody] StoryViewRequest viewRequest)
        {
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{viewRequest.story_id}:{viewRequest.viewer_id}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Fetch the story to check its owner
            var story = await _context.Stories.FirstOrDefaultAsync(s => s.story_id == viewRequest.story_id);

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
                // User has already viewed this story, do not count it again
                return Ok(new { message = "Story already viewed by this user." });
            }

            // Record the view
            var storyView = new storyviews
            {
                story_id = viewRequest.story_id,
                viewer_id = viewRequest.viewer_id,
            };

            _context.StoryViews.Add(storyView);

            // Increment the viewscount in the stories table
            story.viewscount++;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Story view recorded successfully." });
        }

        // GET: api/Stories/{storyId}/viewers
        [HttpGet("{storyId}/viewers")]
        public async Task<IActionResult> GetStoryViewers(int storyId)
        {
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{storyId}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Fetch all users who viewed the story
            var viewers = await _context.StoryViews
                                        .Where(v => v.story_id == storyId)
                                        .Include(v => v.viewer) // Include viewer information
                                        .Select(v => new StoryViewerResponse
                                        {
                                            viewer_id = v.viewer_id,
                                            fullname = v.viewer.fullname,
                                            profile_pic = v.viewer.profile_pic,
                                            viewed_at = v.viewedat
                                        })
                                        .ToListAsync();

            // Return the list of viewers
            return Ok(viewers);
        }

        // DELETE: api/Stories/Media/{storyMediaId}/{userId}
        [HttpDelete("Media/{storyMediaId}/{userId}")]
        public async Task<IActionResult> DeleteStoryMedia(int storyMediaId, int userId)
        {
            // Validate the signature from headers
            string signature = Request.Headers["X-Signature"];
            var dataToSign = $"{storyMediaId}:{userId}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Fetch the story media from the database using the primary key
            var storyMedia = await _context.StoriesMedia.FindAsync(storyMediaId);

            // If story media is not found, return NotFound
            if (storyMedia == null)
            {
                return NotFound("Story media not found.");
            }

            // Fetch the story that this media belongs to
            var story = await _context.Stories.FindAsync(storyMedia.story_id);

            // Check if the story belongs to the provided userId
            if (story == null || story.user_id != userId)
            {
                return Forbid("You are not authorized to delete this story media.");
            }

            // Delete the story media
            _context.StoriesMedia.Remove(storyMedia);

            // Save changes to the database
            await _context.SaveChangesAsync();

            return Ok("Story media deleted successfully.");
        }

    }
}
