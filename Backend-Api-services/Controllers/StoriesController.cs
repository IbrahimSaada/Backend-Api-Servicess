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

            var story = new stories
            {
                user_id = storyRequest.user_id,

            };

            if (storyRequest.Media != null && storyRequest.Media.Any())
            {
                story.Media = storyRequest.Media.Select(m => new storiesmedia
                {
                    media_url = m.media_url,
                    media_type = m.media_type,
                    stories = story
                }).ToList();
            }

            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStory), new { id = story.story_id }, story);
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
            // Fetch all stories including their media
            var stories = await _context.Stories
                                        .Include(s => s.Media)
                                        .ToListAsync();

            // Create the response list
            var responseList = new List<StoriesResponse>();

            foreach (var story in stories)
            {
                // Check if the story has been viewed by the specified user
                bool isViewed = await _context.StoryViews
                                              .AnyAsync(v => v.story_id == story.story_id && v.viewer_id == userId);

                var response = new StoriesResponse
                {
                    story_id = story.story_id,
                    user_id = story.user_id,
                    createdat = story.createdat,
                    expiresat = story.expiresat,
                    isactive = story.isactive,
                    viewscount = story.viewscount,
                    isviewed = isViewed,
                    Media = story.Media.Select(m => new StoriesMediaResponse
                    {
                        media_id = m.media_id,
                        media_url = m.media_url,
                        media_type = m.media_type
                    }).ToList()
                };

                responseList.Add(response);
            }

            return Ok(responseList);
        }
    }
}
