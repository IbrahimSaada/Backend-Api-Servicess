using Backend_Api_services.Models.DTOs_Admin;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers.Controller_Admin
{
    public class BanController : ControllerBase
    {
        private readonly IBanService _banService;
        public BanController(IBanService banService)
        {
            _banService = banService;
        }
        [HttpPost("BanUser")]
        public async Task<IActionResult> BanUser([FromBody] BanUserRequest request)
        {
            var success = await _banService.BanUserAsync(request.UserId, request.BanReason, request.ExpiresAt);
            if (!success) return NotFound("User not found.");
            return Ok("User banned successfully.");
        }

        [HttpPost("UnbanUser/{userId}")]
        public async Task<IActionResult> UnbanUser(int userId)
        {
            var success = await _banService.UnbanUserAsync(userId);
            if (!success) return NotFound("No active bans found for the user.");
            return Ok("User unbanned successfully.");
        }
    }
}
