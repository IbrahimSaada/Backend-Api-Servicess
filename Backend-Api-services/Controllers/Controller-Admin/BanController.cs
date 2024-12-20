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

        [HttpGet("GetAllBannedUsers")]
        public async Task<IActionResult> GetAllBannedUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page <= 0 || pageSize <= 0) return BadRequest("Page and page size must be greater than zero.");

            var (bannedUsers, totalCount) = await _banService.GetAllBannedUsersAsync(page, pageSize);
            if (!bannedUsers.Any()) return NotFound("No banned users found.");

            var result = new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                BannedUsers = bannedUsers
            };

            return Ok(result);
        }

    }
}
