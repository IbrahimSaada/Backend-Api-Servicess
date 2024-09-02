using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        private readonly apiDbContext _context;

        public StoriesController(apiDbContext context)
        {
            _context = context;
        }

        // POST: api/Stories
        [HttpPost]
        public async Task<IActionResult> PostStory([FromBody] StoriesRequest storyRequest)
        {
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
                                 expiresat = m.expiresat // Include expiresat for frontend reference
                             }).ToList()
            }).Where(r => r.Media.Any()) // Only return stories that have at least one unexpired media item
            .ToList();

            return Ok(responseList);
        }



    }
}
