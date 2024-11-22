using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
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
    }
}
