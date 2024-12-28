using Backend_Api_services.Models.DTOs_Admin;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers.Controller_Admin
{
    [Authorize]
    public class BanController : ControllerBase
    {
        private readonly IBanService _banService;
        private readonly SignatureService _signatureService;
        private readonly ILogger<BanController> _logger;
        public BanController(IBanService banService, SignatureService signatureService, ILogger<BanController> logger)
        {
            _banService = banService;
            _signatureService = signatureService;
            _logger = _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        [HttpPost("BanUser")]
        public async Task<IActionResult> BanUser([FromBody] BanUserRequest request)
        {
            // Extract the signature from the request header
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // Use ISO 8601 or round-trip "o" here
            var dataToSign = $"{request.UserId}|{request.BanReason}|{request.ExpiresAt?.ToString("yyyy-MM-ddTHH:mm:ss.fff")}";


            _logger.LogError("Backend Sig: " + signature);
            _logger.LogError("Backend dataToSign: " + dataToSign);

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var success = await _banService.BanUserAsync(request.UserId, request.BanReason, request.ExpiresAt);
            if (!success) return NotFound("User not found.");
            return Ok("User banned successfully.");
        }


        [HttpPost("UnbanUser/{userId}")]
        public async Task<IActionResult> UnbanUser(int userId)
        {
            // Extract the signature from the request header
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            var dataToSign = $"{userId}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var success = await _banService.UnbanUserAsync(userId);
            if (!success) return NotFound("No active bans found for the user.");
            return Ok("User unbanned successfully.");
        }

        [HttpGet("GetAllBannedUsers")]
        public async Task<IActionResult> GetAllBannedUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Extract the signature from the request header
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            var dataToSign = $"Page:{page}|PageSize:{pageSize}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

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
