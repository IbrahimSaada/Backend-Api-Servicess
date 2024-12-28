using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

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
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            return Ok(notifications);
        }

        // PUT: api/notification/mark-as-read/{notificationId}
        [HttpPut("mark-as-read/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            await _notificationService.MarkAsReadAsync(notificationId);
            return NoContent();
        }

        [HttpGet("unread-count/{userId}")]
        public async Task<IActionResult> GetUnreadCount(int userId)
        {
            // Count the notifications where recipient_user_id = userId AND is_read = false
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = unreadCount });
        }

        // PUT: api/notification/mark-all-as-read/{userId}
        [HttpPut("mark-all-as-read/{userId}")]
        public async Task<IActionResult> MarkAllAsRead(int userId)
        {
            await _notificationService.MarkAllAsReadAsync(userId);
            return NoContent();
        }

        [HttpPost("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId, [FromQuery] int userId)
        {
            // Attempt to delete the notification
            var success = await _notificationService.DeleteNotificationAsync(notificationId, userId);

            if (!success)
            {
                // If deletion fails, either the notification didn't exist or the user was unauthorized
                return Unauthorized("Unable to delete notification. You may not be the notification’s recipient.");
            }

            // Successfully deleted
            return NoContent();
        }

    }
}
