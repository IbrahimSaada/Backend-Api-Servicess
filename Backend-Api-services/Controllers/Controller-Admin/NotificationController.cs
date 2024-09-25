using Backend_Api_services.Models.DTOs;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    // POST: api/notifications/send
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Body))
        {
            return BadRequest("Invalid request data");
        }

        // Create a new Firebase message
        var message = new Message
        {
            Token = request.Token,
            Notification = new Notification
            {
                Title = request.Title,
                Body = request.Body
            }
        };

        try
        {
            // Send the message using Firebase Admin SDK
            string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            return Ok(new { MessageId = response });
        }
        catch (FirebaseMessagingException ex)
        {
            return StatusCode(500, $"Error sending notification: {ex.Message}");
        }
    }
}
