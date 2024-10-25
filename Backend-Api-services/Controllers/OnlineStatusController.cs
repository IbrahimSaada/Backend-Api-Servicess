using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs.OnlineStatusDTO;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OnlineStatusController : ControllerBase
    {
        private readonly apiDbContext _context;

        public OnlineStatusController(apiDbContext context)
        {
            _context = context;
        }
        /*
        // Update online status,this endpoint is depracted
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateOnlineStatusDto dto)
        {
            var status = await _context.OnlineStatus.FindAsync(dto.UserId);

            if (status == null)
            {
                status = new Online_Status
                {
                    user_id = dto.UserId,
                    is_online = dto.IsOnline,
                    last_seen = dto.IsOnline ? (DateTime?)null : DateTime.UtcNow
                };
                _context.OnlineStatus.Add(status);
            }
            else
            {
                status.is_online = dto.IsOnline;
                status.last_seen = dto.IsOnline ? (DateTime?)null : DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        */

        
        // Get online status for a user
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetStatus(int userId)
        {
            var status = await _context.OnlineStatus.FindAsync(userId);
            if (status == null)
            {
                return NotFound();
            }

            var result = new OnlineStatusDto
            {
                UserId = status.user_id,
                IsOnline = status.is_online,
                LastSeen = status.last_seen
            };

            return Ok(result);
        }
    }
}
