// ChatNotificationService.cs
using System.Threading.Tasks;
using Backend_Api_services.Services.Interfaces;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Services
{
    public class ChatNotificationService : IChatNotificationService
    {
        private readonly INotificationService _notificationService;
        private readonly apiDbContext _context;
        private readonly ILogger<ChatNotificationService> _logger;

        public ChatNotificationService(INotificationService notificationService, apiDbContext context, ILogger<ChatNotificationService> logger)
        {
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        public async Task NotifyUserOfNewMessageAsync(int recipientUserId, int senderUserId, string messageContent)
        {
            // Retrieve the recipient user from the database
            var recipient = await _context.users.FindAsync(recipientUserId);
            if (recipient == null || string.IsNullOrEmpty(recipient.fcm_token))
            {
                _logger.LogWarning($"User {recipientUserId} not found or has no FCM token.");
                return;
            }

            // Check if the sender is muted by the recipient (for chat-specific notifications)
            bool isChatMuted = await _context.muted_users
                .AnyAsync(m => m.muted_by_user_id == recipientUserId && m.muted_user_id == senderUserId);

            if (isChatMuted)
            {
                _logger.LogInformation($"Chat notifications are muted for user {recipientUserId} from user {senderUserId}. Skipping notification.");
                return;
            }

            // Retrieve the recipient user
            var recipientUser = await _context.users.FirstOrDefaultAsync(u => u.user_id == recipientUserId);

            // Check if the recipient has globally muted notifications
            if (recipientUser?.is_notifications_muted == true)
            {
                _logger.LogInformation($"Notifications are globally muted for user {recipientUserId}. Skipping notification.");
                return;
            }

            // Retrieve the sender's information for the notification message
            var sender = await _context.users.FindAsync(senderUserId);
            string senderName = sender?.fullname ?? "Someone";

            // Prepare the notification request
            var notificationRequest = new NotificationRequest
            {
                Token = recipient.fcm_token,
                Title = "New Message",
                Body = $"{senderName} sent you a new message."
            };

            // Attempt to send the push notification
            try
            {
                await _notificationService.SendNotificationAsync(notificationRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send chat notification to user {recipientUserId}.");
            }
        }
    }
}