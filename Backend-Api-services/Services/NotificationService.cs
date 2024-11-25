using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Services
{
    public class NotificationService : INotificationService
    {
        private readonly apiDbContext _context;

        public NotificationService(apiDbContext context)
        {
            _context = context;
        }

        public async Task SendNotificationAsync(NotificationRequest request)
        {
            var message = new Message
            {
                Token = request.Token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = request.Title,
                    Body = request.Body
                }
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                // Optionally log the response or handle it as needed
            }
            catch (FirebaseMessagingException ex)
            {
                // Handle exceptions appropriately
                throw new Exception($"Error sending notification: {ex.Message}", ex);
            }
        }

        public async Task CreateNotificationAsync(Models.Entities.Notification notification)
        {
            // Insert the notification into the database
            _context.notification.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Models.Entities.Notification>> GetUserNotificationsAsync(int userId)
        {
            var notifications = await _context.notification
                .Where(n => n.recipient_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .ToListAsync();

            return notifications;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            // Update the notification's is_read status in the database
            var notification = await _context.notification.FindAsync(notificationId);
            if (notification != null)
            {
                notification.is_read = true;
                await _context.SaveChangesAsync();
            }
        }

        // **New Method to Send and Save Notifications**
        public async Task SendAndSaveNotificationAsync(int recipientUserId, int senderUserId, string type, int? relatedEntityId, string message)
        {
            // Retrieve the recipient user
            var recipientUser = await _context.users
                .FirstOrDefaultAsync(u => u.user_id == recipientUserId);

            if (recipientUser != null && !string.IsNullOrEmpty(recipientUser.fcm_token))
            {
                // Create a notification entry in the database
                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = type,
                    related_entity_id = relatedEntityId,
                    message = message,
                    created_at = DateTime.UtcNow
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Prepare the notification request
                var notificationRequest = new NotificationRequest
                {
                    Token = recipientUser.fcm_token,
                    Title = $"New {type}",
                    Body = message
                };

                // Send the push notification
                await SendNotificationAsync(notificationRequest);
            }
        }
    }
}
