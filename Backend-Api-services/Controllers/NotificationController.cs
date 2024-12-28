using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Services;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [CheckBan]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly SignatureService _signatureService;

        public NotificationController(INotificationService notificationService, SignatureService signatureService)
        {
            _notificationService = notificationService;
            _signatureService = signatureService;   
        }
        // POST: api/notification/send
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
        {
            if (string.IsNullOrEmpty(request.Token) ||
                string.IsNullOrEmpty(request.Title) ||
                string.IsNullOrEmpty(request.Body))
            {
                return BadRequest("Invalid request data.");
            }

            try
            {
                await _notificationService.SendNotificationAsync(request);
                return Ok(new { Message = "Notification sent successfully." });
            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, $"Error sending notification: {ex.Message}");
            }
        }

        // GET: api/notification/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserNotifications(int userId)
        {
            // 1. Extract signature from header
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // 2. Build dataToSign with userId
            var dataToSign = userId.ToString(); // e.g. "123"

            // 3. Validate signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // If validation passes, proceed
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            return Ok(notifications);
        }

        // PUT: api/notification/mark-as-read/{notificationId}
        [HttpPut("mark-as-read/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(int notificationId, [FromQuery] int userId)
        {
            // 1. Extract signature
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // 2. Combine userId and notificationId in some manner
            var dataToSign = $"{userId}:{notificationId}";

            // 3. Validate
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // If OK, proceed
            await _notificationService.MarkAsReadAsync(notificationId);
            return NoContent();
        }

        // GET: api/notification/unread-count/{userId}
        [HttpGet("unread-count/{userId}")]
        public async Task<IActionResult> GetUnreadCount(int userId)
        {
            // 1. Extract signature
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // 2. Build dataToSign
            var dataToSign = userId.ToString();

            // 3. Validate
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = unreadCount });
        }

        // PUT: api/notification/mark-all-as-read/{userId}
        [HttpPut("mark-all-as-read/{userId}")]
        public async Task<IActionResult> MarkAllAsRead(int userId)
        {
            // 1. Extract signature
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // 2. dataToSign
            var dataToSign = userId.ToString();

            // 3. Validate
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            await _notificationService.MarkAllAsReadAsync(userId);
            return NoContent();
        }

        // DELETE (or POST) to delete a notification: api/notification/{notificationId}?userId=123
        [HttpPost("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId, [FromQuery] int userId)
        {
            // 1. Extract signature
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // 2. Combine userId & notificationId for the signature
            var dataToSign = $"{userId}:{notificationId}";

            // 3. Validate
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Attempt to delete the notification
            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);
            if (!success)
            {
                return Unauthorized("Unable to delete notification. You may not be the notification’s recipient or it does not exist.");
            }

            return NoContent();
        }
    }
}